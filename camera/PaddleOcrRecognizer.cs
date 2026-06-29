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

            using var prepared = PreprocessLine(lineBgr);
            var inputData = BuildInputTensor(prepared);

            var inputShape = new[] { 1, 3, InputHeight, MaxInputWidth };
            var inputTensor = new DenseTensor<float>(inputData, inputShape);
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(_inputName, inputTensor)
            };

            using var outputs = _session.Run(inputs);
            var logits = outputs[0].AsTensor<float>();
            string raw = DecodeCtcWithRepeats(logits);

            return isProvinceLine ? NormalizeProvince(raw) : ThaiPlateNumberNormalizer.Normalize(raw);
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

        private static Mat PreprocessLine(Mat lineBgr)
        {
            var result = lineBgr.Clone();

            int targetHeight = result.Height < 24 ? 64 : 48;
            if (result.Height < targetHeight)
            {
                double scale = targetHeight / (double)result.Height;
                Cv2.Resize(result, result, new Size(), scale, scale, InterpolationFlags.Cubic);
            }

            using var gray = new Mat();
            Cv2.CvtColor(result, gray, ColorConversionCodes.BGR2GRAY);
            using var clahe = Cv2.CreateCLAHE(clipLimit: 2.5, tileGridSize: new Size(8, 8));
            clahe.Apply(gray, gray);
            Cv2.CvtColor(gray, result, ColorConversionCodes.GRAY2BGR);

            return result;
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
        /// หา local maximum ของแต่ละ class ตาม time axis แทนการ collapse แบบ greedy
        /// </summary>
        private string DecodeCtcWithRepeats(Tensor<float> logits)
        {
            int timeSteps = logits.Dimensions[1];
            if (timeSteps == 0)
                return string.Empty;

            const float minPeakScore = -2f;
            // gap เล็ก = รวม peak ซ้ำใน glyph เดียว; ไม่รวมเลขซ้ำคนละตำแหน่ง (6688 ต้องได้ 4 หลัก)
            const int minPeakGap = 4;

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
                return DecodeCtcStandard(logits);

            // merge peaks ใกล้กันเกินไปของ class เดียวกัน (noise) — เก็บ peak ที่ score สูงกว่า
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

            if (!string.IsNullOrEmpty(peakText))
                return peakText;

            return DecodeCtcStandard(logits);
        }

        /// <summary>CTC greedy มาตรฐาน — fallback</summary>
        private string DecodeCtcStandard(Tensor<float> logits)
        {
            int timeSteps = logits.Dimensions[1];
            var text = new StringBuilder();
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
                        text.Append(_alphabet[dictIdx]);
                }

                prev = bestIdx;
            }

            return text.ToString().Trim();
        }

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
