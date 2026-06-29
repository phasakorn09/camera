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

            _session = new InferenceSession(modelPath, new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
            });

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
            PlateImagePreprocessor.SplitPlateLines(prepared, out _, out _, out int splitY);

            Mat preview = prepared.Clone();
            if (splitY > 0 && splitY < preview.Height)
            {
                Cv2.Line(preview, new Point(0, splitY), new Point(preview.Width - 1, splitY),
                    new Scalar(0, 255, 255), 1, LineTypes.AntiAlias);
            }

            return (RecognizePreparedPlate(prepared), preview);
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
                string plateNumber = RecognizeLine(topLine, isProvinceLine: false);
                string provinceRaw = RecognizeLine(bottomLine, isProvinceLine: true);
                string province = ThaiProvinceMatcher.Match(provinceRaw);

                return new ThaiPlateReadResult
                {
                    PlateNumber = plateNumber,
                    Province = province
                };
            }
            finally
            {
                topLine.Dispose();
                bottomLine.Dispose();
            }
        }

        public string Recognize(Mat plateCropBgr) =>
            RecognizeThaiPlate(plateCropBgr).FullText;

        private string RecognizeLine(Mat lineBgr, bool isProvinceLine)
        {
            if (lineBgr.Empty() || lineBgr.Height < 4 || lineBgr.Width < 4)
                return string.Empty;

            var (text, score) = TryRecognizeLineOnce(lineBgr, isProvinceLine);
            if (IsStrongRead(text, score))
                return text;

            using var sharpLine = PlateImagePreprocessor.SharpenForOcr(lineBgr);
            var (sharpText, sharpScore) = TryRecognizeLineOnce(sharpLine, isProvinceLine);
            if (sharpScore > score)
                return sharpText;

            if (string.IsNullOrWhiteSpace(text))
                return sharpText;

            return text;
        }

        private static bool IsStrongRead(string text, float score) =>
            !string.IsNullOrWhiteSpace(text) && text.Length >= 2 && score >= -1.2f;

        private (string Text, float Score) TryRecognizeLineOnce(Mat lineBgr, bool isProvinceLine)
        {
            using var prepared = PreprocessLine(lineBgr);
            bool blurry = PlateImagePreprocessor.IsBlurry(lineBgr);

            using var outputs = RunInference(prepared);
            var logits = outputs[0].AsTensor<float>();

            var (peakText, peakScore, mergedPeaks) = DecodeCtcWithRepeats(logits, blurry);
            var (stdText, stdScore) = DecodeCtcStandardWithScore(logits);

            string raw = PickBestRawDecode(peakText, peakScore, stdText, stdScore);
            float score = Math.Max(peakScore, stdScore);

            if (!isProvinceLine && mergedPeaks != null && mergedPeaks.Count > 0)
                raw = ResolvePlateLineAmbiguity(logits, mergedPeaks, raw);

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

            const float margin = 2.2f;

            for (int i = 0; i < peaks.Count; i++)
            {
                char current = ClassIndexToChar(peaks[i].ClassIdx);
                if (current == '\0')
                    continue;

                foreach (char alt in ThaiPlateLetterConfusion.GetPartners(current))
                {
                    int altIdx = CharToClassIndex(alt);
                    if (altIdx <= 0)
                        continue;

                    float altLogit = logits[0, peaks[i].Time, altIdx];
                    if (altLogit < peaks[i].Score - margin)
                        continue;

                    var trialPeaks = peaks.ToList();
                    trialPeaks[i] = (peaks[i].Time, altIdx, altLogit);
                    string trialRaw = BuildTextFromPeaks(trialPeaks);
                    string trialNorm = ThaiPlateNumberNormalizer.Normalize(trialRaw);
                    float trialScore = ScorePlateLineCandidate(trialNorm, trialPeaks);

                    if (trialScore > bestScore && !string.IsNullOrWhiteSpace(trialNorm))
                    {
                        bestScore = trialScore;
                        bestRaw = trialRaw;
                        bestNorm = trialNorm;
                    }
                }
            }

            return bestRaw;
        }

        private static float ScorePlateLineCandidate(
            string normalized,
            List<(int Time, int ClassIdx, float Score)> peaks)
        {
            if (string.IsNullOrWhiteSpace(normalized))
                return float.MinValue;

            int letterCount = normalized.TakeWhile(c => c != ' ').Count(c => c >= '\u0E01' && c <= '\u0E2E');
            if (letterCount == 0)
                letterCount = normalized.Count(c => c >= '\u0E01' && c <= '\u0E2E');

            float peakAvg = peaks.Count > 0 ? peaks.Average(p => p.Score) : -5f;
            return peakAvg + letterCount * 0.35f + normalized.Length * 0.05f;
        }

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

        private static Mat PreprocessLine(Mat lineBgr)
        {
            var result = lineBgr.Clone();

            int targetHeight = result.Height < 24 ? 64 : 48;
            if (result.Height < targetHeight)
            {
                double scale = targetHeight / (double)result.Height;
                Cv2.Resize(result, result, new Size(), scale, scale, InterpolationFlags.Cubic);
            }

            if (PlateImagePreprocessor.IsBlurry(result))
            {
                using var denoised = PlateImagePreprocessor.DenoiseForOcr(result);
                denoised.CopyTo(result);
            }

            using var gray = new Mat();
            Cv2.CvtColor(result, gray, ColorConversionCodes.BGR2GRAY);
            double clip = PlateImagePreprocessor.IsBlurry(result) ? 3.0 : 2.5;
            using var clahe = Cv2.CreateCLAHE(clipLimit: clip, tileGridSize: new Size(8, 8));
            clahe.Apply(gray, gray);
            Cv2.CvtColor(gray, result, ColorConversionCodes.GRAY2BGR);

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
            Tensor<float> logits, bool blurryImage)
        {
            int timeSteps = logits.Dimensions[1];
            if (timeSteps == 0)
                return (string.Empty, float.MinValue, null);

            float minPeakScore = blurryImage ? -3.0f : -2.0f;
            int minPeakGap = blurryImage ? 3 : 4;

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
