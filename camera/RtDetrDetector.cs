using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace ConsoleApp1
{
    /// <summary>
    /// เก็บผลลัพธ์การตรวจจับ 1 กล่อง (พิกัด pixel บนภาพต้นฉบับ + confidence)
    /// </summary>
    public class Detection
    {
        public Rect Box { get; set; }
        public float Confidence { get; set; }
        public int ClassId { get; set; }
        /// <summary>บรรทัดบน: อักษรไทย + เลขทะเบียน</summary>
        public string PlateNumber { get; set; } = string.Empty;
        /// <summary>บรรทัดล่าง: ชื่อจังหวัด</summary>
        public string Province { get; set; } = string.Empty;
        /// <summary>ข้อความรวมสำหรับแสดงผล</summary>
        public string PlateText { get; set; } = string.Empty;

        public bool HasOcrResult =>
            !string.IsNullOrWhiteSpace(PlateNumber) || !string.IsNullOrWhiteSpace(Province);

        /// <summary>กำลังเก็บเฟรม / กำลัง OCR — ใช้แสดงสถานะบนจอ</summary>
        public PlateCaptureUiPhase CapturePhase { get; set; } = PlateCaptureUiPhase.None;
        public int CollectFrameCount { get; set; }
        public int CollectTargetFrames { get; set; }
        public double CollectSharpness { get; set; }
        internal int CaptureTrackId { get; set; }
    }

    /// <summary>
    /// Wrapper สำหรับโมเดล RT-DETRv2 (plate_rtdetr.onnx)
    /// 
    /// ความต่างจาก YOLOv8n ที่สำคัญ:
    ///  - Input node ชื่อ "pixel_values" (fixed)
    ///  - Preprocessing: resize ธรรมดา (ไม่ต้อง letterbox) + /255 เท่านั้น ไม่มี mean/std normalization
    ///  - Output 2 node: "logits" [1,300,1] (ต้อง sigmoid) และ "pred_boxes" [1,300,4] (cx,cy,w,h normalized [0,1])
    ///  - Confidence threshold ~0.08 + กรองรูปทรงป้าย (aspect ratio / ขนาดสัมพัทธ์)
    ///  - NMS ไม่จำเป็นสำหรับ RT-DETR แต่ใส่ไว้กันกล่องซ้อนในกรณีภาพที่มีป้ายหลายอัน
    /// </summary>
    public class RtDetrDetector : IDisposable
    {
        private readonly InferenceSession _session;
        private readonly int _inputWidth;
        private readonly int _inputHeight;

        // RT-DETR single-class ให้ score ต่ำ (ป้ายจริงมัก ~0.10–0.20)
        private const float ConfidenceThreshold = 0.08f;
        private const float LowConfidenceCutoff = 0.12f;
        private const float NmsIouThreshold = 0.45f;
        private const int MaxDetectionsPerFrame = 5;

        // ป้ายไทยกว้างกว่าสูง — รถ ~4:1, มอเตอร์ไซค์ ~1.5:1
        private const float MinAspectRatio = 1.4f;
        private const float MaxAspectRatio = 7.0f;
        private const float MinRelativeWidth = 0.04f;
        private const float MinRelativeHeight = 0.012f;
        private const float MaxRelativeWidth = 0.80f;
        private const float MaxRelativeHeight = 0.45f;

        public RtDetrDetector(string modelPath, int inputWidth = 640, int inputHeight = 640)
        {
            _inputWidth = inputWidth;
            _inputHeight = inputHeight;

            var options = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
            };

            _session = new InferenceSession(modelPath, options);
        }

        /// <summary>
        /// รับภาพ 1 เฟรม คืนค่ารายการกล่องป้ายทะเบียนที่ตรวจพบ (ผ่าน NMS แล้ว)
        /// </summary>
        public List<Detection> Detect(Mat frame)
        {
            // Preprocessing: resize ธรรมดา (ไม่ใช้ letterbox เพราะโมเดลให้พิกัดแบบ normalized)
            // ขนาดภาพต้นฉบับไม่มีผลต่อความแม่นยำของพิกัด เพราะโมเดล output เป็น [0,1] เสมอ
            using var resized = new Mat();
            Cv2.Resize(frame, resized, new Size(_inputWidth, _inputHeight));

            var inputTensor = MatToTensor(resized);

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("pixel_values", inputTensor)
            };

            using var results = _session.Run(inputs);

            // อ่าน output ทั้ง 2 node โดย match ชื่อเผื่อ runtime คืนค่าไม่เรียงลำดับเสมอไป
            Tensor<float>? logits = null;
            Tensor<float>? predBoxes = null;

            foreach (var r in results)
            {
                if (r.Name == "logits")      logits    = r.AsTensor<float>();
                else if (r.Name == "pred_boxes") predBoxes = r.AsTensor<float>();
            }

            if (logits == null || predBoxes == null)
            {
                Console.WriteLine("[RtDetrDetector] ไม่พบ output node ที่คาดหวัง ('logits' / 'pred_boxes')");
                return new List<Detection>();
            }

            var detections = ParseOutput(logits, predBoxes, frame.Width, frame.Height);
            var nms = ApplyNms(detections);

            if (nms.Count <= MaxDetectionsPerFrame)
                return nms;

            return nms.Take(MaxDetectionsPerFrame).ToList();
        }

        /// <summary>
        /// แปลง Mat (BGR, uint8) เป็น Tensor รูปแบบ NCHW (RGB, float32, 0.0–1.0)
        /// ใช้ unsafe pointer เพื่อเข้าถึง memory ของ Mat โดยตรง เร็วกว่า .At<Vec3b>() มาก
        /// เพราะ .At<> มี overhead จาก marshaling ทุกครั้งที่เรียก (409,600 ครั้ง/เฟรม สำหรับ 640×640)
        /// </summary>
        private unsafe DenseTensor<float> MatToTensor(Mat image)
        {
            int h = image.Height, w = image.Width;
            var tensor = new DenseTensor<float>(new[] { 1, 3, h, w });
            var span   = tensor.Buffer.Span;
            int channelSize = h * w;

            byte* basePtr = (byte*)image.DataPointer;
            int stride    = (int)image.Step();

            for (int y = 0; y < h; y++)
            {
                byte* row = basePtr + y * stride;
                int rowOffset = y * w;
                for (int x = 0; x < w; x++)
                {
                    int idx = rowOffset + x;
                    // OpenCV เก็บเป็น BGR → แปลงเป็น RGB ตามที่โมเดล RT-DETR ต้องการ
                    span[idx]                    = row[x * 3 + 2] / 255f; // R
                    span[channelSize     + idx]  = row[x * 3 + 1] / 255f; // G
                    span[2 * channelSize + idx]  = row[x * 3 + 0] / 255f; // B
                }
            }

            return tensor;
        }

        /// <summary>
        /// แปลง output ของโมเดลเป็นรายการ Detection บนพิกัดภาพต้นฉบับ
        /// 
        /// logits    : [1, 300, 1] — raw score ต้อง sigmoid ก่อนใช้
        /// pred_boxes: [1, 300, 4] — cx, cy, w, h แบบ normalized [0,1] เทียบกับขนาด input
        /// </summary>
        private List<Detection> ParseOutput(
            Tensor<float> logits,
            Tensor<float> predBoxes,
            int originalWidth,
            int originalHeight)
        {
            var detections = new List<Detection>();
            int numCandidates = logits.Dimensions[1]; // 300

            for (int i = 0; i < numCandidates; i++)
            {
                // Sigmoid: แปลง raw logit → probability [0,1]
                float score = 1f / (1f + MathF.Exp(-logits[0, i, 0]));

                if (score < ConfidenceThreshold) continue;

                // พิกัด normalized cx,cy,w,h [0,1] → แปลงเป็น pixel บนภาพต้นฉบับ
                float cx = predBoxes[0, i, 0];
                float cy = predBoxes[0, i, 1];
                float bw = predBoxes[0, i, 2];
                float bh = predBoxes[0, i, 3];

                float x1 = (cx - bw / 2f) * originalWidth;
                float y1 = (cy - bh / 2f) * originalHeight;
                float boxW = bw * originalWidth;
                float boxH = bh * originalHeight;

                // กันกล่องล้นขอบ
                int finalX = Math.Clamp((int)x1,   0, originalWidth  - 1);
                int finalY = Math.Clamp((int)y1,   0, originalHeight - 1);
                int finalW = Math.Clamp((int)boxW, 1, originalWidth  - finalX);
                int finalH = Math.Clamp((int)boxH, 1, originalHeight - finalY);

                var box = new Rect(finalX, finalY, finalW, finalH);
                if (!IsPlausiblePlateBox(box, originalWidth, originalHeight, score))
                    continue;

                detections.Add(new Detection
                {
                    Box        = box,
                    Confidence = score,
                    ClassId    = 0 // โมเดลนี้มีแค่ class 0 = license_plate
                });
            }

            return detections;
        }

        /// <summary>กรอง false positive — รูปทรง/ขนาดต้องใกล้เคียงป้ายทะเบียนไทย</summary>
        private static bool IsPlausiblePlateBox(Rect box, int frameW, int frameH, float confidence)
        {
            if (box.Width < 24 || box.Height < 8)
                return false;

            float relW = (float)box.Width / frameW;
            float relH = (float)box.Height / frameH;

            if (relW < MinRelativeWidth || relH < MinRelativeHeight)
                return false;
            if (relW > MaxRelativeWidth || relH > MaxRelativeHeight)
                return false;

            float aspect = (float)box.Width / box.Height;
            if (aspect < MinAspectRatio || aspect > MaxAspectRatio)
                return false;

            // confidence ต่ำ → ต้องมีสัดส่วนชัด (ป้ายรถ) มากกว่าป้ายมอเตอร์ไซค์
            if (confidence < LowConfidenceCutoff && aspect < 2.0f)
                return false;

            return true;
        }

        /// <summary>
        /// NMS (optional สำหรับ RT-DETR แต่มีประโยชน์เมื่อภาพมีป้ายหลายอัน)
        /// เรียงจาก confidence สูงไปต่ำ แล้วตัดกล่องที่ IoU เกิน threshold ออก
        /// </summary>
        private List<Detection> ApplyNms(List<Detection> detections)
        {
            var result    = new List<Detection>();
            var sorted    = detections.OrderByDescending(d => d.Confidence).ToList();
            var suppressed = new bool[sorted.Count];

            for (int i = 0; i < sorted.Count; i++)
            {
                if (suppressed[i]) continue;
                result.Add(sorted[i]);

                for (int j = i + 1; j < sorted.Count; j++)
                {
                    if (suppressed[j]) continue;
                    if (CalculateIoU(sorted[i].Box, sorted[j].Box) > NmsIouThreshold)
                        suppressed[j] = true;
                }
            }

            return result;
        }

        private float CalculateIoU(Rect a, Rect b)
        {
            int x1 = Math.Max(a.X,           b.X);
            int y1 = Math.Max(a.Y,           b.Y);
            int x2 = Math.Min(a.X + a.Width, b.X + b.Width);
            int y2 = Math.Min(a.Y + a.Height,b.Y + b.Height);

            int interW    = Math.Max(0, x2 - x1);
            int interH    = Math.Max(0, y2 - y1);
            int interArea = interW * interH;
            int unionArea = a.Width * a.Height + b.Width * b.Height - interArea;

            return unionArea <= 0 ? 0f : (float)interArea / unionArea;
        }

        public void Dispose() => _session?.Dispose();
    }
}
