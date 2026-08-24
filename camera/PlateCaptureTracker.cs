using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenCvSharp;

namespace ConsoleApp1
{
    /// <summary>
    /// ติดตามป้ายด้วย IoU → เก็บเฟรม → เลือก crop ชัดสุด → OCR ครั้งเดียว → บันทึกรูป
    /// </summary>
    internal sealed class PlateCaptureTracker : IDisposable
    {
        private const float MatchIou = 0.35f;
        // ให้สอดคล้องกับ RT-DETR (~0.08+) — คุณภาพกรองที่ sharpness ตอน OCR แทน
        private const float MinDetectConfidence = 0.08f;
        private const int MaxTracks = 8;
        private const int MaxMissFrames = 5;
        private const int MaxCollectFrames = 8;
        private const int MinCollectFrames = 2;
        private const int PlateauFrames = 2;
        private const double MinSharpnessForOcr = 20.0;
        private const double GoodSharpnessEarly = 35.0;
        private const double ExcellentSharpnessEarly = 45.0;
        private const double ImageChangeMeanThreshold = 12.0;
        private const int DedupLogSeconds = 12;
        private const int DigitAnchorSeconds = 25;
        private const int MaxCaptureFiles = 300;
        private const int JpegSaveQuality = 85;

        private readonly List<CaptureTrack> _tracks = new();
        private readonly Dictionary<string, (DateTime UtcTime, float Score)> _recentLogKeys = new(StringComparer.Ordinal);
        private readonly Dictionary<string, DigitAnchorEntry> _digitAnchors = new(StringComparer.Ordinal);
        private readonly string _capturesDir;
        private readonly PlateCaptureCsvLogger _csvLogger;
        private int _saveCounter;
        private int _nextTrackId = 1;

        public PlateCaptureTracker()
        {
            _capturesDir = Path.Combine(AppContext.BaseDirectory, "Captures");
            Directory.CreateDirectory(_capturesDir);
            _csvLogger = new PlateCaptureCsvLogger(_capturesDir);
            PruneCaptureFolderIfNeeded();
        }

        public string CsvLogPath => _csvLogger.CsvPath;

        public string CapturesDirectory => _capturesDir;
        public int MaxCollectFrameTarget => MaxCollectFrames;

        public IReadOnlyList<PlateCaptureResult> ProcessFrame(
            Mat frame,
            IList<Detection> detections,
            PaddleOcrRecognizer ocr)
        {
            var finalized = new List<PlateCaptureResult>();
            var matched = new HashSet<CaptureTrack>();

            foreach (var det in detections)
            {
                det.CapturePhase = PlateCaptureUiPhase.None;
                det.CollectFrameCount = 0;
                det.CollectTargetFrames = MaxCollectFrames;
                det.CollectSharpness = 0;
                det.CaptureTrackId = 0;

                if (det.Confidence < MinDetectConfidence)
                    continue;

                var track = FindOrCreateTrack(frame, det.Box);
                matched.Add(track);
                track.MissCount = 0;
                track.LastBox = det.Box;
                det.CaptureTrackId = track.Id;

                if (track.IsFinalized)
                {
                    ApplyTrackToDetection(det, track);
                    continue;
                }

                ConsiderCandidate(frame, det.Box, det.Confidence, track);

                if (!track.IsFinalized && track.BestCrop != null && ShouldTryFinalize(track))
                {
                    if (TryFinalizeTrack(track, ocr, out var result))
                        finalized.Add(result);
                }

                ApplyTrackToDetection(det, track);
            }

            foreach (var track in _tracks)
            {
                if (matched.Contains(track))
                    continue;

                track.MissCount++;

                if (track.IsFinalized || track.BestCrop == null)
                    continue;

                if (track.MissCount >= MaxMissFrames)
                {
                    if (IsReadyForOcr(track) && TryFinalizeTrack(track, ocr, out var result))
                        finalized.Add(result);
                    else
                        track.MarkDiscarded();
                }
            }

            _tracks.RemoveAll(t => t.IsDiscarded || t.MissCount > MaxMissFrames);
            PruneOverflowTracks();

            return finalized;
        }

        private void ApplyTrackToDetection(Detection det, CaptureTrack track)
        {
            if (track.IsFinalized)
            {
                if (track.DisplaySnapshot is { } snapshot)
                {
                    if (snapshot.IsValidated)
                    {
                        det.PlateNumber = snapshot.Plate;
                        det.Province = snapshot.Province;
                        det.PlateText = BuildPlateText(snapshot.Plate, snapshot.Province);
                        det.CapturePhase = PlateCaptureUiPhase.None;
                    }
                    else
                    {
                        det.PlateNumber = string.Empty;
                        det.Province = string.Empty;
                        det.PlateText = string.Empty;
                        det.CapturePhase = PlateCaptureUiPhase.UncertainRead;
                    }

                    return;
                }

                det.CapturePhase = PlateCaptureUiPhase.ProcessingOcr;
                det.CollectFrameCount = track.FrameCount;
                det.CollectTargetFrames = MaxCollectFrames;
                det.CollectSharpness = track.BestSharpness;
                return;
            }

            det.PlateNumber = string.Empty;
            det.Province = string.Empty;
            det.PlateText = string.Empty;
            det.CapturePhase = PlateCaptureUiPhase.Collecting;
            det.CollectFrameCount = track.FrameCount;
            det.CollectTargetFrames = MaxCollectFrames;
            det.CollectSharpness = track.BestSharpness;
        }

        private static string BuildPlateText(string plate, string province)
        {
            if (string.IsNullOrWhiteSpace(plate))
                return province ?? string.Empty;
            if (string.IsNullOrWhiteSpace(province))
                return plate;
            return $"{plate} | {province}";
        }

        private void ConsiderCandidate(Mat frame, Rect box, float detectConfidence, CaptureTrack track)
        {
            using var crop = PlateImagePreprocessor.CropPlateForOcr(frame, box);
            if (crop.Empty() || crop.Width < 8 || crop.Height < 8)
                return;

            track.FrameCount++;

            double sharpness = PlateImagePreprocessor.MeasureSharpnessScore(crop);
            double score = sharpness + detectConfidence * 40.0;

            if (track.BestCrop != null && score <= track.BestScore)
            {
                track.PlateauCount++;
                return;
            }

            track.PlateauCount = 0;
            track.BestScore = score;
            track.BestSharpness = sharpness;
            track.BestConfidence = detectConfidence;
            track.BestBox = box;
            track.BestCrop?.Dispose();
            track.BestCrop = crop.Clone();
        }

        private bool ShouldTryFinalize(CaptureTrack track)
        {
            if (track.FrameCount >= MaxCollectFrames)
                return true;

            if (track.BestSharpness >= ExcellentSharpnessEarly &&
                track.FrameCount >= MinCollectFrames &&
                (track.PlateauCount >= 1 || track.FrameCount >= 3))
                return true;

            if (track.BestSharpness >= GoodSharpnessEarly &&
                track.FrameCount >= MinCollectFrames &&
                track.PlateauCount >= PlateauFrames)
                return true;

            return track.FrameCount >= MinCollectFrames && track.PlateauCount >= PlateauFrames;
        }

        private static bool IsReadyForOcr(CaptureTrack track)
        {
            if (track.BestCrop == null)
                return false;

            if (track.BestSharpness < MinSharpnessForOcr)
                return false;

            if (track.FrameCount >= MinCollectFrames)
                return true;

            return track.BestSharpness >= GoodSharpnessEarly;
        }

        private bool TryFinalizeTrack(CaptureTrack track, PaddleOcrRecognizer ocr, out PlateCaptureResult result)
        {
            result = default;

            if (!IsReadyForOcr(track))
            {
                track.MarkDiscarded();
                return false;
            }

            track.IsFinalized = true;

            var (ocrResult, previewImage) = ocr.RecognizeCropWithPreview(track.BestCrop!);
            ocrResult = ApplyDigitAnchor(ocrResult);

            bool isValidated = ThaiPlateResultValidator.IsValid(ocrResult.PlateNumber, out _);
            bool shouldLog = isValidated && ShouldLogResult(
                ocrResult.PlateNumber,
                ocrResult.Province,
                ocrResult.ReadQualityScore);
            string savedPath = shouldLog
                ? SaveCaptureImage(track.BestCrop!)
                : string.Empty;

            track.DisplaySnapshot = new DisplaySnapshot(
                ocrResult.PlateNumber,
                ocrResult.Province,
                track.LastBox,
                isValidated);

            result = new PlateCaptureResult(
                track.LastBox,
                track.Id,
                ocrResult.PlateNumber,
                ocrResult.Province,
                savedPath,
                track.BestConfidence,
                track.BestSharpness,
                ocrResult.ReadQualityScore,
                track.FrameCount,
                isValidated,
                shouldLog,
                previewImage);

            _csvLogger.Append(result);

            return true;
        }

        private string SaveCaptureImage(Mat crop)
        {
            string fileName = $"plate_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{++_saveCounter}.jpg";
            string savedPath = Path.Combine(_capturesDir, fileName);
            Cv2.ImWrite(savedPath, crop, new[] { (int)ImwriteFlags.JpegQuality, JpegSaveQuality });
            PruneCaptureFolderIfNeeded();
            return savedPath;
        }

        private void PruneCaptureFolderIfNeeded()
        {
            string[] files;
            try
            {
                files = Directory.GetFiles(_capturesDir, "plate_*.jpg");
            }
            catch (IOException)
            {
                return;
            }

            if (files.Length <= MaxCaptureFiles)
                return;

            Array.Sort(files, (a, b) =>
            {
                int timeCompare = File.GetLastWriteTimeUtc(a).CompareTo(File.GetLastWriteTimeUtc(b));
                return timeCompare != 0 ? timeCompare : string.CompareOrdinal(a, b);
            });

            int removeCount = files.Length - MaxCaptureFiles;
            for (int i = 0; i < removeCount; i++)
            {
                try
                {
                    File.Delete(files[i]);
                }
                catch (IOException)
                {
                    // ข้ามไฟล์ที่ลบไม่ได้ — ไม่ block OCR
                }
            }
        }

        private bool ShouldLogResult(string plate, string province, float qualityScore)
        {
            if (string.IsNullOrWhiteSpace(plate))
                return false;

            string key = BuildLogKey(plate, province);
            if (_recentLogKeys.TryGetValue(key, out var lastLogged) &&
                (DateTime.UtcNow - lastLogged.UtcTime).TotalSeconds < DedupLogSeconds &&
                qualityScore <= lastLogged.Score + 0.05f)
                return false;

            _recentLogKeys[key] = (DateTime.UtcNow, qualityScore);
            PruneRecentLogKeys();
            return true;
        }

        private ThaiPlateReadResult ApplyDigitAnchor(ThaiPlateReadResult result)
        {
            string digits = PlatePrefixScorer.ExtractDigitPart(result.PlateNumber);
            if (digits.Length < 3)
                return result;

            string key = string.IsNullOrWhiteSpace(result.Province)
                ? digits
                : $"{digits}|{result.Province.Trim()}";

            var now = DateTime.UtcNow;
            PruneDigitAnchors(now);

            if (_digitAnchors.TryGetValue(key, out DigitAnchorEntry prev))
            {
                string newDigits = PlatePrefixScorer.ExtractDigitPart(result.PlateNumber);
                string prevDigits = PlatePrefixScorer.ExtractDigitPart(prev.Plate);

                if (newDigits.Length == 4 && prevDigits.Length == 3
                    && ThaiPlateNumberNormalizer.IsLikelyVanityExpansionFromThree(newDigits, prevDigits)
                    && prev.Score >= result.ReadQualityScore - 0.15f)
                {
                    return new ThaiPlateReadResult
                    {
                        PlateNumber = prev.Plate,
                        Province = prev.Province,
                        ReadQualityScore = prev.Score
                    };
                }

                if (prev.Score > result.ReadQualityScore + 0.05f)
                {
                    return new ThaiPlateReadResult
                    {
                        PlateNumber = prev.Plate,
                        Province = prev.Province,
                        ReadQualityScore = prev.Score
                    };
                }
            }

            if (!_digitAnchors.TryGetValue(key, out prev) || result.ReadQualityScore >= prev.Score)
            {
                _digitAnchors[key] = new DigitAnchorEntry(
                    result.PlateNumber,
                    result.Province,
                    result.ReadQualityScore,
                    now);
            }

            return result;
        }

        private void PruneDigitAnchors(DateTime now)
        {
            var expired = _digitAnchors
                .Where(kv => (now - kv.Value.UtcTime).TotalSeconds > DigitAnchorSeconds * 2)
                .Select(kv => kv.Key)
                .ToList();

            foreach (string key in expired)
                _digitAnchors.Remove(key);
        }

        private void PruneRecentLogKeys()
        {
            var expired = _recentLogKeys
                .Where(kv => (DateTime.UtcNow - kv.Value.UtcTime).TotalSeconds > DedupLogSeconds * 2)
                .Select(kv => kv.Key)
                .ToList();

            foreach (string key in expired)
                _recentLogKeys.Remove(key);
        }

        private static string BuildLogKey(string plate, string province)
        {
            string digits = PlatePrefixScorer.ExtractDigitPart(plate);
            string core = digits.Length >= 3 ? digits : plate.Trim();
            return string.IsNullOrWhiteSpace(province) ? core : $"{core}|{province.Trim()}";
        }

        private CaptureTrack FindOrCreateTrack(Mat frame, Rect box)
        {
            var existing = FindBestTrack(box, preferActive: true);

            if (existing != null && existing.MissCount == 0)
            {
                if (existing.IsFinalized)
                {
                    if (HasPlateImageChanged(frame, box, existing))
                        return ReplaceTrack(existing, box);

                    return existing;
                }

                return existing;
            }

            if (existing != null)
            {
                existing.Dispose();
                _tracks.Remove(existing);
            }

            return CreateTrack(box);
        }

        private CaptureTrack ReplaceTrack(CaptureTrack oldTrack, Rect box)
        {
            oldTrack.Dispose();
            _tracks.Remove(oldTrack);
            return CreateTrack(box);
        }

        private CaptureTrack CreateTrack(Rect box)
        {
            var created = new CaptureTrack { Id = _nextTrackId++, LastBox = box };
            _tracks.Add(created);
            PruneOverflowTracks();
            return created;
        }

        private static bool HasPlateImageChanged(Mat frame, Rect box, CaptureTrack track)
        {
            if (track.BestCrop == null || track.BestCrop.Empty())
                return false;

            using var crop = PlateImagePreprocessor.CropPlateForOcr(frame, box);
            if (crop.Empty())
                return false;

            if (Math.Abs(crop.Width - track.BestCrop.Width) > 8 ||
                Math.Abs(crop.Height - track.BestCrop.Height) > 8)
                return true;

            using var current = new Mat();
            using var saved = new Mat();
            Cv2.Resize(crop, current, new Size(64, 32));
            Cv2.Resize(track.BestCrop, saved, new Size(64, 32));
            Cv2.CvtColor(current, current, ColorConversionCodes.BGR2GRAY);
            Cv2.CvtColor(saved, saved, ColorConversionCodes.BGR2GRAY);

            using var diff = new Mat();
            Cv2.Absdiff(current, saved, diff);
            return Cv2.Mean(diff).Val0 > ImageChangeMeanThreshold;
        }

        private CaptureTrack? FindBestTrack(Rect box, bool preferActive = false)
        {
            CaptureTrack? best = null;
            float bestIou = MatchIou;

            foreach (var track in _tracks)
            {
                if (preferActive && track.IsDiscarded)
                    continue;

                float iou = CalculateIoU(track.LastBox, box);
                if (iou > bestIou)
                {
                    bestIou = iou;
                    best = track;
                }
            }

            return best;
        }

        private void PruneOverflowTracks()
        {
            while (_tracks.Count > MaxTracks)
            {
                var oldest = _tracks
                    .OrderBy(t => t.IsFinalized ? 1 : 0)
                    .ThenBy(t => t.FrameCount)
                    .First();
                oldest.Dispose();
                _tracks.Remove(oldest);
            }
        }

        private static float CalculateIoU(Rect a, Rect b)
        {
            int x1 = Math.Max(a.X, b.X);
            int y1 = Math.Max(a.Y, b.Y);
            int x2 = Math.Min(a.X + a.Width, b.X + b.Width);
            int y2 = Math.Min(a.Y + a.Height, b.Y + b.Height);

            int interW = Math.Max(0, x2 - x1);
            int interH = Math.Max(0, y2 - y1);
            int interArea = interW * interH;
            int unionArea = a.Width * a.Height + b.Width * b.Height - interArea;

            return unionArea <= 0 ? 0f : (float)interArea / unionArea;
        }

        public void Dispose()
        {
            foreach (var track in _tracks)
                track.Dispose();
            _tracks.Clear();
            _recentLogKeys.Clear();
        }

        private sealed class CaptureTrack : IDisposable
        {
            public int Id;
            public Rect LastBox;
            public int MissCount;
            public int FrameCount;
            public int PlateauCount;
            public bool IsFinalized;
            public bool Discarded;
            public double BestScore;
            public double BestSharpness;
            public float BestConfidence;
            public Rect BestBox;
            public Mat? BestCrop;
            public DisplaySnapshot? DisplaySnapshot;

            public bool IsDiscarded => Discarded;

            public void MarkDiscarded() => Discarded = true;

            public void Dispose() => BestCrop?.Dispose();
        }

        private readonly record struct DisplaySnapshot(string Plate, string Province, Rect Box, bool IsValidated);

        private readonly record struct DigitAnchorEntry(
            string Plate,
            string Province,
            float Score,
            DateTime UtcTime);
    }

    internal readonly record struct PlateCaptureResult(
        Rect Box,
        int TrackId,
        string PlateNumber,
        string Province,
        string SavedImagePath,
        float DetectConfidence,
        double SharpnessScore,
        float ReadQualityScore,
        int FramesCollected,
        bool IsValidated,
        bool ShouldLog,
        Mat? PreviewImage);

    public enum PlateCaptureUiPhase
    {
        None,
        Collecting,
        ProcessingOcr,
        UncertainRead
    }

    internal readonly record struct PlateCaptureUiState(
        PlateCaptureUiPhase Phase,
        int FrameCount,
        int MaxFrames,
        double BestSharpness);
}
