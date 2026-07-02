using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace ConsoleApp1
{
    /// <summary>
    /// PaddleOCR PP-OCRv5 recognition-only — ออกแบบสำหรับป้ายทะเบียนไทย 2 บรรทัด
    /// </summary>
    public class PaddleOcrRecognizer : IDisposable
    {
        private readonly InferenceSession _session;
        private readonly string _inputName;
        private readonly int _outputChars;
        private readonly List<string> _alphabet;

        private const int InputHeight = 48;
        // กว้างขึ้น = time step มากขึ้น ลดการอ่านขาดตัวท้าย (6กท 6688)
        private const int MaxInputWidth = 640;

        public PaddleOcrRecognizer(string modelPath, string dictPath)
        {
            if (!File.Exists(modelPath))
                throw new FileNotFoundException($"ไม่พบโมเดล OCR: {modelPath}");
            if (!File.Exists(dictPath))
                throw new FileNotFoundException($"ไม่พบ dictionary OCR: {dictPath}");

            _session = OnnxSessionFactory.Create(modelPath);

            _inputName = _session.InputMetadata.Keys.FirstOrDefault()
                ?? throw new InvalidOperationException("โมเดล OCR ไม่มี input node");

            var outputMeta = _session.OutputMetadata.Values.First();
            _outputChars = outputMeta.Dimensions[^1];

            _alphabet = LoadDictionary(dictPath);
        }

        public ThaiPlateReadResult RecognizeThaiPlateFromFrame(Mat frame, Rect plateBox, int padding = 10)
        {
            var (result, _) = RecognizeThaiPlateFromFrameWithPreview(frame, plateBox);
            return result;
        }

        /// <summary>OCR + คืนภาพที่ prepare แล้ว (เส้นเหลือง = จุด split บรรทัด)</summary>
        public (ThaiPlateReadResult Result, Mat? PreviewImage) RecognizeThaiPlateFromFrameWithPreview(
            Mat frame, Rect plateBox)
        {
            using var crop = PlateImagePreprocessor.CropPlateForOcr(frame, plateBox);
            if (crop.Empty() || crop.Width < 8 || crop.Height < 8)
                return (new ThaiPlateReadResult(), null);

            using var prepared = PlateImagePreprocessor.PreparePlateCropForOcr(crop);
            return (RecognizePreparedPlate(prepared), BuildPreparedPreview(prepared));
        }

        /// <summary>OCR จาก crop ใน memory (ไม่ round-trip ไฟล์)</summary>
        public (ThaiPlateReadResult Result, Mat? PreviewImage) RecognizeCropWithPreview(Mat cropBgr)
        {
            if (cropBgr == null || cropBgr.Empty() || cropBgr.Width < 8 || cropBgr.Height < 8)
                return (new ThaiPlateReadResult(), null);

            using var prepared = PlateImagePreprocessor.PreparePlateCropForOcr(cropBgr);
            return (RecognizePreparedPlate(prepared), BuildPreparedPreview(prepared));
        }

        /// <summary>OCR จากไฟล์ crop ที่บันทึกไว้ (หลัง finalize ป้าย)</summary>
        public (ThaiPlateReadResult Result, Mat? PreviewImage) RecognizeSavedCropWithPreview(string imagePath)
        {
            using var crop = Cv2.ImRead(imagePath);
            return RecognizeCropWithPreview(crop);
        }

        private static Mat BuildPreparedPreview(Mat prepared)
        {
            PlateImagePreprocessor.SplitPlateLines(prepared, out _, out _, out int splitY);

            Mat preview = prepared.Clone();
            if (splitY > 0 && splitY < preview.Height)
            {
                Cv2.Line(preview, new Point(0, splitY), new Point(preview.Width - 1, splitY),
                    new Scalar(0, 255, 255), 1, LineTypes.AntiAlias);
            }

            return preview;
        }

        public ThaiPlateReadResult RecognizeThaiPlate(Mat plateCropBgr)
        {
            using var prepared = PlateImagePreprocessor.PreparePlateCropForOcr(plateCropBgr);
            return RecognizePreparedPlate(prepared);
        }

        private ThaiPlateReadResult RecognizePreparedPlate(Mat preparedPlate)
        {
            PlateImagePreprocessor.SplitPlateLines(preparedPlate, out Mat topLine, out Mat bottomLine, out _);

            try
            {
                var (plateNumber, plateScore) = RecognizePlateNumberLine(topLine);
                string province = RecognizeProvinceLine(bottomLine, plateNumber);

                float qualityScore = plateScore;
                if (!string.IsNullOrWhiteSpace(province))
                    qualityScore += 0.3f;

                return new ThaiPlateReadResult
                {
                    PlateNumber = plateNumber,
                    Province = province,
                    ReadQualityScore = qualityScore
                };
            }
            finally
            {
                topLine.Dispose();
                bottomLine.Dispose();
            }
        }

        private string RecognizeProvinceLine(Mat bottomLine, string plateNumber)
        {
            if (bottomLine.Empty() || bottomLine.Height < 4 || bottomLine.Width < 4)
                return string.Empty;

            string provinceRaw = RecognizeLine(bottomLine, isProvinceLine: true);
            string province = ThaiProvinceMatcher.Match(provinceRaw);

            if (!string.IsNullOrWhiteSpace(province) || string.IsNullOrWhiteSpace(plateNumber))
                return province;

            using (var enhanced = PlateImagePreprocessor.EnhanceProvinceLineForOcr(bottomLine))
            {
                string retryRaw = RecognizeLine(enhanced, isProvinceLine: true);
                province = ThaiProvinceMatcher.Match(retryRaw);
                if (!string.IsNullOrWhiteSpace(province))
                    return province;
            }

            using var sharpLine = PlateImagePreprocessor.SharpenForOcr(bottomLine, amount: 0.55);
            string sharpRaw = RecognizeLine(sharpLine, isProvinceLine: true);
            return ThaiProvinceMatcher.Match(sharpRaw);
        }

        public string Recognize(Mat plateCropBgr) =>
            RecognizeThaiPlate(plateCropBgr).FullText;

        private (string Text, float Score) RecognizePlateNumberLine(Mat topLine)
        {
            if (topLine.Empty() || topLine.Height < 4 || topLine.Width < 4)
                return (string.Empty, float.MinValue);

            var (fullText, fullScore) = RecognizeLineDualPass(topLine, emphasizeLetters: false);

            using var prefixStrip = PlateImagePreprocessor.ExtractPlatePrefixStrip(topLine);
            var (prefixText, prefixScore) = RecognizeLineDualPass(prefixStrip, emphasizeLetters: true);

            string merged = MergePlateOcrReads(fullText, fullScore, prefixText, prefixScore);
            float mergedScore = Math.Max(fullScore, prefixScore);
            if (!string.IsNullOrWhiteSpace(merged))
                mergedScore += PlatePrefixScorer.ScorePlateNumber(merged) * 0.5f;

            return (merged, mergedScore);
        }

        private (string Text, float Score) RecognizeLineDualPass(Mat lineBgr, bool emphasizeLetters)
        {
            var (text, score) = TryRecognizeLineOnce(lineBgr, isProvinceLine: false, emphasizeLetters);
            using var sharpLine = PlateImagePreprocessor.SharpenForOcr(lineBgr);
            var (sharpText, sharpScore) = TryRecognizeLineOnce(sharpLine, isProvinceLine: false, emphasizeLetters);

            if (sharpScore > score)
                return (sharpText, sharpScore);

            if (string.IsNullOrWhiteSpace(text))
                return (sharpText, sharpScore);

            return (text, score);
        }

        private static string MergePlateOcrReads(
            string full,
            float fullScore,
            string prefixFocus,
            float prefixScore)
        {
            SplitPlateParts(full, out string fullLetters, out string fullDigits);
            SplitPlateParts(prefixFocus, out string prefixLetters, out string prefixDigits);

            int fullConsonants = CountThaiConsonants(fullLetters);
            int prefixConsonants = CountThaiConsonants(prefixLetters);

            string letters = fullLetters;
            if (prefixConsonants >= 1 && prefixScore >= fullScore - 0.8f)
            {
                if (fullConsonants < 2)
                    letters = prefixLetters;
                else if (fullConsonants > 2 && prefixConsonants <= fullConsonants)
                    letters = prefixLetters;
                else if (prefixConsonants >= 2 && prefixConsonants > fullConsonants)
                    letters = prefixLetters;
                else if (prefixLetters != fullLetters && prefixScore >= fullScore - 0.5f)
                    letters = prefixScore > fullScore ? prefixLetters : fullLetters;
            }
            else if (prefixConsonants >= 2)
            {
                if (fullConsonants < 2)
                    letters = prefixLetters;
                else if (prefixConsonants > fullConsonants)
                    letters = prefixLetters;
                else if (prefixLetters != fullLetters && prefixScore >= fullScore - 0.6f)
                    letters = prefixScore > fullScore ? prefixLetters : fullLetters;
            }

            string digits = PickBestMergedDigits(fullDigits, prefixDigits, fullScore, prefixScore, letters);

            if (string.IsNullOrEmpty(letters) && string.IsNullOrEmpty(digits))
                return string.Empty;
            if (string.IsNullOrEmpty(digits))
                return ThaiPlateNumberNormalizer.Normalize(letters);
            if (string.IsNullOrEmpty(letters))
                return ThaiPlateNumberNormalizer.Normalize(digits);

            return ThaiPlateNumberNormalizer.Normalize($"{letters} {digits}");
        }

        private static string PickBestMergedDigits(
            string fullDigits,
            string prefixDigits,
            float fullScore,
            float prefixScore,
            string letters)
        {
            if (string.IsNullOrEmpty(fullDigits))
                return prefixDigits;
            if (string.IsNullOrEmpty(prefixDigits))
                return fullDigits;

            if (fullDigits == prefixDigits)
                return fullDigits;

            if (fullDigits.Length == 4 && prefixDigits.Length == 3
                && ThaiPlateNumberNormalizer.IsLikelyVanityExpansionFromThree(fullDigits, prefixDigits))
                return prefixDigits;

            if (fullDigits.Length == 4 && prefixDigits.Length == 2
                && prefixDigits == fullDigits[..2]
                && (letters == "ฐฐ" || ThaiPlateLetterConfusion.IsThoThoLikePrefix(letters)))
                return prefixDigits;

            if (prefixDigits.Length < fullDigits.Length)
                return fullDigits;

            if (prefixDigits.Length > fullDigits.Length)
                return prefixDigits;

            return fullScore >= prefixScore ? fullDigits : prefixDigits;
        }

        private static void SplitPlateParts(string normalized, out string letters, out string digits)
        {
            letters = string.Empty;
            digits = string.Empty;

            if (string.IsNullOrWhiteSpace(normalized))
                return;

            normalized = normalized.Trim();
            int space = normalized.IndexOf(' ');
            if (space > 0)
            {
                letters = normalized[..space].Trim();
                digits = normalized[(space + 1)..].Trim();
                return;
            }

            var letterSb = new StringBuilder();
            var digitSb = new StringBuilder();
            foreach (char c in normalized)
            {
                if (c >= '0' && c <= '9')
                    digitSb.Append(c);
                else if (c >= '\u0E01' && c <= '\u0E2E')
                    letterSb.Append(c);
            }

            letters = letterSb.ToString();
            digits = digitSb.ToString();
        }

        private static int CountThaiConsonants(string text) =>
            text.Count(c => c >= '\u0E01' && c <= '\u0E2E');

        private string RecognizeLine(Mat lineBgr, bool isProvinceLine)
        {
            if (lineBgr.Empty() || lineBgr.Height < 4 || lineBgr.Width < 4)
                return string.Empty;

            var (text, score) = TryRecognizeLineOnce(lineBgr, isProvinceLine, emphasizeLetters: false);
            if (IsStrongRead(text, score))
                return text;

            using var sharpLine = PlateImagePreprocessor.SharpenForOcr(lineBgr);
            var (sharpText, sharpScore) = TryRecognizeLineOnce(sharpLine, isProvinceLine, emphasizeLetters: false);
            if (sharpScore > score)
                return sharpText;

            if (string.IsNullOrWhiteSpace(text))
                return sharpText;

            return text;
        }

        private static bool IsStrongRead(string text, float score) =>
            !string.IsNullOrWhiteSpace(text) && text.Length >= 2 && score >= -1.2f;

        private (string Text, float Score) TryRecognizeLineOnce(
            Mat lineBgr,
            bool isProvinceLine,
            bool emphasizeLetters = false)
        {
            using var prepared = PreprocessLine(lineBgr, emphasizeLetters);
            bool blurry = PlateImagePreprocessor.IsBlurry(lineBgr);
            bool plateTopLine = !isProvinceLine;

            using var outputs = RunInference(prepared);
            var logits = outputs[0].AsTensor<float>();

            var (peakText, peakScore, mergedPeaks) = DecodeCtcWithRepeats(logits, blurry, plateTopLine);
            var (stdText, stdScore) = DecodeCtcStandardWithScore(logits);

            string raw = PickBestRawDecode(peakText, peakScore, stdText, stdScore);
            float score = Math.Max(peakScore, stdScore);

            if (!isProvinceLine && mergedPeaks != null && mergedPeaks.Count > 0)
            {
                raw = ResolvePlateLineAmbiguity(logits, mergedPeaks, raw);
                string plateNorm = ThaiPlateNumberNormalizer.Normalize(raw);
                score = ScorePlateLineCandidate(plateNorm, mergedPeaks);
                return (plateNorm, score);
            }

            string normalized = isProvinceLine
                ? NormalizeProvince(raw)
                : ThaiPlateNumberNormalizer.Normalize(raw);

            if (!string.IsNullOrWhiteSpace(normalized))
                return (normalized, score + normalized.Length * 0.05f);

            return (normalized, score);
        }

        private IDisposableReadOnlyCollection<DisposableNamedOnnxValue> RunInference(Mat prepared)
        {
            var inputData = BuildInputTensor(prepared);
            var inputShape = new[] { 1, 3, InputHeight, MaxInputWidth };
            var inputTensor = new DenseTensor<float>(inputData, inputShape);
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(_inputName, inputTensor)
            };
            return _session.Run(inputs);
        }

        private static string PickBestRawDecode(string peak, float peakScore, string standard, float stdScore)
        {
            if (string.IsNullOrEmpty(peak))
                return standard;
            if (string.IsNullOrEmpty(standard))
                return peak;

            if (peak.Length > standard.Length && peakScore >= stdScore - 0.8f)
                return peak;
            if (standard.Length > peak.Length && stdScore >= peakScore - 0.8f)
                return standard;

            return peakScore >= stdScore ? peak : standard;
        }

        /// <summary>ลองสลับพยัญชนะที่คล้ายกัน (ฆ/ค/ก) เมื่อ logit ใกล้เคียง</summary>
        private string ResolvePlateLineAmbiguity(
            Tensor<float> logits,
            List<(int Time, int ClassIdx, float Score)> peaks,
            string initialRaw)
        {
            string bestRaw = initialRaw;
            string bestNorm = ThaiPlateNumberNormalizer.Normalize(initialRaw);
            float bestScore = ScorePlateLineCandidate(bestNorm, peaks);

            const float defaultMargin = 2.2f;
            const float prefixMargin = 1.55f;
            const float thoToMargin = 2.55f;
            const float thoKBonus = 0.45f;

            for (int i = 0; i < peaks.Count; i++)
            {
                char current = ClassIndexToChar(peaks[i].ClassIdx);
                if (current == '\0')
                    continue;

                bool isPrefixPeak = i < 3 && IsThaiConsonantChar(current);
                float margin = isPrefixPeak ? prefixMargin : defaultMargin;

                foreach (char alt in ThaiPlateLetterConfusion.GetPartners(current))
                {
                    int altIdx = CharToClassIndex(alt);
                    if (altIdx <= 0)
                        continue;

                    float altLogit = logits[0, peaks[i].Time, altIdx];
                    float effectiveMargin = margin;
                    if (alt == 'ฒ' && current == 'ต')
                        effectiveMargin = Math.Max(effectiveMargin, thoToMargin);
                    if (alt == 'ฎ' && current == 'ด')
                        effectiveMargin = Math.Max(effectiveMargin, 2.45f);
                    if (alt == 'ฏ' && current is 'ต' or 'ร')
                        effectiveMargin = Math.Max(effectiveMargin, 2.45f);

                    if (altLogit < peaks[i].Score - effectiveMargin)
                        continue;

                    var trialPeaks = peaks.ToList();
                    trialPeaks[i] = (peaks[i].Time, altIdx, altLogit);
                    string trialRaw = BuildTextFromPeaks(trialPeaks);
                    string trialNorm = ThaiPlateNumberNormalizer.Normalize(trialRaw);
                    float trialScore = ScorePlateLineCandidate(trialNorm, trialPeaks);

                    if (i == 0 && alt == 'ฒ' && PlatePrefixScorer.ExtractPrefixConsonants(trialNorm) == "ฒก")
                        trialScore += thoKBonus;
                    if (i == 0 && alt is 'ฎ' or 'ฏ')
                        trialScore += 0.25f;

                    if (trialScore > bestScore && !string.IsNullOrWhiteSpace(trialNorm))
                    {
                        bestScore = trialScore;
                        bestRaw = trialRaw;
                        bestNorm = trialNorm;
                    }
                }
            }

            return TryPreferThoKOverToK(logits, peaks, bestRaw, bestNorm, bestScore);
        }

        /// <summary>เมื่อ OCR ได้ ตก แต่ logit ฒ ใกล้ ต มาก — ลอง ฒก (ไม่สลับทุกที่)</summary>
        private string TryPreferThoKOverToK(
            Tensor<float> logits,
            List<(int Time, int ClassIdx, float Score)> peaks,
            string bestRaw,
            string bestNorm,
            float bestScore)
        {
            if (PlatePrefixScorer.ExtractPrefixConsonants(bestNorm) != "ตก" || peaks.Count == 0)
                return bestRaw;

            int thoIdx = CharToClassIndex('ฒ');
            if (thoIdx <= 0)
                return bestRaw;

            float thoLogit = logits[0, peaks[0].Time, thoIdx];
            if (thoLogit < peaks[0].Score - 2.55f)
                return bestRaw;

            var trialPeaks = peaks.ToList();
            trialPeaks[0] = (peaks[0].Time, thoIdx, thoLogit);
            string trialRaw = BuildTextFromPeaks(trialPeaks);
            string trialNorm = ThaiPlateNumberNormalizer.Normalize(trialRaw);
            if (PlatePrefixScorer.ExtractPrefixConsonants(trialNorm) != "ฒก")
                return bestRaw;

            float trialScore = ScorePlateLineCandidate(trialNorm, trialPeaks) + 0.45f;
            return trialScore > bestScore ? trialRaw : bestRaw;
        }

        private static float ScorePlateLineCandidate(
            string normalized,
            List<(int Time, int ClassIdx, float Score)> peaks)
        {
            if (string.IsNullOrWhiteSpace(normalized))
                return float.MinValue;

            float peakAvg = peaks.Count > 0 ? peaks.Average(p => p.Score) : -5f;
            return peakAvg + PlatePrefixScorer.ScorePlateNumber(normalized, peaks) + normalized.Length * 0.02f;
        }

        private static bool IsThaiConsonantChar(char c) => c >= '\u0E01' && c <= '\u0E2E';

        private int CharToClassIndex(char c)
        {
            string target = c.ToString();
            for (int i = 0; i < _alphabet.Count; i++)
            {
                if (_alphabet[i] == target)
                    return i + 1;
            }

            return 0;
        }

        private char ClassIndexToChar(int classIdx)
        {
            int dictIdx = classIdx - 1;
            if (dictIdx < 0 || dictIdx >= _alphabet.Count || _alphabet[dictIdx].Length != 1)
                return '\0';

            return _alphabet[dictIdx][0];
        }

        private static Mat PreprocessLine(Mat lineBgr, bool emphasizeLetters = false)
        {
            var result = lineBgr.Clone();

            int targetHeight = emphasizeLetters
                ? 72
                : result.Height < 24 ? 64 : 48;

            if (result.Height < targetHeight)
            {
                double scale = targetHeight / (double)result.Height;
                Cv2.Resize(result, result, new Size(), scale, scale, InterpolationFlags.Cubic);
            }

            if (PlateImagePreprocessor.IsBlurry(result) || emphasizeLetters)
            {
                using var denoised = PlateImagePreprocessor.DenoiseForOcr(result);
                denoised.CopyTo(result);
            }

            using var gray = new Mat();
            Cv2.CvtColor(result, gray, ColorConversionCodes.BGR2GRAY);
            double clip = emphasizeLetters ? 2.8 : PlateImagePreprocessor.IsBlurry(result) ? 3.0 : 2.5;
            using var clahe = Cv2.CreateCLAHE(clipLimit: clip, tileGridSize: new Size(8, 8));
            clahe.Apply(gray, gray);
            Cv2.CvtColor(gray, result, ColorConversionCodes.GRAY2BGR);

            if (emphasizeLetters)
            {
                using var sharp = PlateImagePreprocessor.SharpenForOcr(result, 0.35);
                sharp.CopyTo(result);
            }

            return result;
        }

        /// <summary>
        /// บรรทัดล่างป้ายไทย: พยัญชนะ + สระ + วรรณยุกต์ เท่านั้น — ไม่มีอังกฤษ/ตัวเลข/สัญลักษณ์
        /// </summary>
        private static string NormalizeProvince(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            var sb = new StringBuilder(raw.Length);
            foreach (char c in raw)
            {
                if (IsThaiScriptForProvince(c))
                    sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>อักษรไทยที่ใช้ในชื่อจังหวัดบนป้าย (ไม่รวมเลขไทย/อังกฤษ)</summary>
        private static bool IsThaiScriptForProvince(char c)
        {
            if (c >= '\u0E01' && c <= '\u0E2E') return true; // พยัญชนะ ก–ฮ
            if (c >= '\u0E30' && c <= '\u0E3A') return true; // สระ ะ ั า ำ ิ ี ฯลฯ
            if (c >= '\u0E40' && c <= '\u0E4E') return true; // เ แ โ ใ ไ + วรรณยุกต์
            return false;
        }

        private static float[] BuildInputTensor(Mat bgrImage)
        {
            using var resized = new Mat();
            int h = bgrImage.Rows;
            int w = bgrImage.Cols;
            float ratio = w / (float)h;
            int resizedW = (int)Math.Ceiling(InputHeight * ratio);
            if (resizedW > MaxInputWidth)
                resizedW = MaxInputWidth;

            Cv2.Resize(bgrImage, resized, new Size(resizedW, InputHeight), interpolation: InterpolationFlags.Linear);

            int channelSize = InputHeight * MaxInputWidth;
            var data = new float[3 * channelSize];

            for (int y = 0; y < InputHeight; y++)
            {
                for (int x = 0; x < resizedW; x++)
                {
                    var pix = resized.At<Vec3b>(y, x);
                    int idx = y * MaxInputWidth + x;
                    data[idx] = (pix.Item2 / 255f - 0.5f) / 0.5f;
                    data[channelSize + idx] = (pix.Item1 / 255f - 0.5f) / 0.5f;
                    data[2 * channelSize + idx] = (pix.Item0 / 255f - 0.5f) / 0.5f;
                }
            }

            return data;
        }

        /// <summary>
        /// Peak-based CTC decode — รองรับตัวอักษร/เลขซ้ำ (เช่น 1122)
        /// </summary>
        private (string Text, float Score, List<(int Time, int ClassIdx, float Score)>? Peaks) DecodeCtcWithRepeats(
            Tensor<float> logits,
            bool blurryImage,
            bool plateTopLine = false)
        {
            int timeSteps = logits.Dimensions[1];
            if (timeSteps == 0)
                return (string.Empty, float.MinValue, null);

            float minPeakScore = blurryImage ? -3.0f : -2.0f;
            int minPeakGap = blurryImage ? 3 : plateTopLine ? 3 : 4;

            var peaks = new List<(int Time, int ClassIdx, float Score)>();

            for (int t = 0; t < timeSteps; t++)
            {
                int bestIdx = 0;
                float bestScore = float.MinValue;
                for (int c = 1; c < _outputChars; c++)
                {
                    float score = logits[0, t, c];
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestIdx = c;
                    }
                }

                if (bestIdx == 0 || bestScore < minPeakScore)
                    continue;

                float prevScore = t > 0 ? logits[0, t - 1, bestIdx] : bestScore;
                float nextScore = t < timeSteps - 1 ? logits[0, t + 1, bestIdx] : bestScore;

                if (bestScore >= prevScore && bestScore >= nextScore)
                    peaks.Add((t, bestIdx, bestScore));
            }

            if (peaks.Count == 0)
            {
                var std = DecodeCtcStandardWithScore(logits);
                return (std.Text, std.Score, null);
            }

            var merged = new List<(int Time, int ClassIdx, float Score)>();
            foreach (var peak in peaks.OrderBy(p => p.Time))
            {
                if (merged.Count > 0)
                {
                    var last = merged[^1];
                    if (peak.ClassIdx == last.ClassIdx && peak.Time - last.Time < minPeakGap)
                    {
                        if (peak.Score > last.Score)
                            merged[^1] = peak;
                        continue;
                    }
                }
                merged.Add(peak);
            }

            string peakText = BuildTextFromPeaks(merged);
            float peakScore = merged.Count > 0 ? merged.Average(p => p.Score) : float.MinValue;

            if (!string.IsNullOrEmpty(peakText))
                return (peakText, peakScore, merged);

            var fallback = DecodeCtcStandardWithScore(logits);
            return (fallback.Text, fallback.Score, null);
        }

        /// <summary>CTC greedy มาตรฐาน — fallback</summary>
        private (string Text, float Score) DecodeCtcStandardWithScore(Tensor<float> logits)
        {
            int timeSteps = logits.Dimensions[1];
            var text = new StringBuilder();
            var scores = new List<float>();
            int prev = -1;

            for (int t = 0; t < timeSteps; t++)
            {
                int bestIdx = 0;
                float bestScore = float.MinValue;
                for (int c = 0; c < _outputChars; c++)
                {
                    float score = logits[0, t, c];
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestIdx = c;
                    }
                }

                if (bestIdx == 0)
                {
                    prev = -1;
                    continue;
                }

                if (bestIdx != prev)
                {
                    int dictIdx = bestIdx - 1;
                    if (dictIdx >= 0 && dictIdx < _alphabet.Count)
                    {
                        text.Append(_alphabet[dictIdx]);
                        scores.Add(bestScore);
                    }
                }

                prev = bestIdx;
            }

            float avg = scores.Count > 0 ? scores.Average() : float.MinValue;
            return (text.ToString().Trim(), avg);
        }

        private string DecodeCtcStandard(Tensor<float> logits) =>
            DecodeCtcStandardWithScore(logits).Text;

        private string BuildTextFromPeaks(List<(int Time, int ClassIdx, float Score)> peaks)
        {
            var sb = new StringBuilder(peaks.Count);
            foreach (var peak in peaks)
            {
                int dictIdx = peak.ClassIdx - 1;
                if (dictIdx >= 0 && dictIdx < _alphabet.Count)
                    sb.Append(_alphabet[dictIdx]);
            }
            return sb.ToString().Trim();
        }

        private static List<string> LoadDictionary(string dictPath)
        {
            var lines = File.ReadAllLines(dictPath);
            var dict = new List<string>(lines.Length + 1);
            foreach (var line in lines)
            {
                if (!string.IsNullOrEmpty(line))
                    dict.Add(line);
            }
            dict.Add(" ");
            return dict;
        }

        public void Dispose() => _session.Dispose();
    }
}
