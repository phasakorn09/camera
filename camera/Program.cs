using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using OpenCvSharp;
using SD = System.Drawing;

namespace ConsoleApp1
{
    internal class Program
    {
        // ── Thread 1 (Grabber) → Main thread: เฟรมทุกอันสำหรับแสดงผล ─────────
        private static Mat?  _displayFrame;
        private static bool  _hasDisplayFrame;
        private static readonly object _displayLock = new();

        // ── Thread 1 (Grabber) → Thread 2 (Detector): เฟรมที่รอ inference ────
        // ถ้า detector ยังไม่ว่าง เฟรมเก่าจะถูกทิ้ง (drop) ทำให้ไม่มี queue สะสม
        private static Mat?  _pendingFrame;
        private static bool  _hasPendingFrame;
        private static readonly object _pendingLock = new();

        // ── Thread 2 (Detector) → Main thread: ผลการตรวจจับล่าสุด ───────────
        // Main thread วาด boxes จาก list นี้บนทุกเฟรมที่แสดง
        private static List<Detection> _lastDetections = new();
        private static List<PlateOcrPreview> _lastOcrPreviews = new();
        private static readonly object _resultsLock = new();

        private const int MaxPreviewOverlay = 2;
        private const int PreviewThumbMaxW = 120;
        private const int PreviewThumbMaxH = 34;
        private const int PreviewMargin = 8;

        private static volatile bool _running = true;

        // ── Sharpness: 0 = ปิด, 1–5 = คมชัดมากขึ้นเรื่อยๆ ────────────────────
        // ปรับด้วยปุ่ม [ และ ] บนหน้าต่างวิดีโอ
        private static volatile int _sharpnessLevel = 0;

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            string modelsDir = System.IO.Path.Combine(AppContext.BaseDirectory, "Models");
            string modelPath = System.IO.Path.Combine(modelsDir, "plate_rtdetr.onnx");
            string ocrModelPath = System.IO.Path.Combine(modelsDir, "th_pp-ocrv5_mobile_rec.onnx");
            string ocrDictPath = System.IO.Path.Combine(modelsDir, "ppocrv5_th_dict.txt");

            if (!System.IO.File.Exists(modelPath))
            {
                Console.WriteLine($"ไม่พบไฟล์โมเดลที่: {modelPath}");
                Console.WriteLine("ตรวจสอบว่าได้วาง plate_rtdetr.onnx ไว้ใน Models/ และตั้งค่า Copy to Output Directory แล้ว");
                Console.ReadKey();
                return;
            }

            if (!System.IO.File.Exists(ocrModelPath) || !System.IO.File.Exists(ocrDictPath))
            {
                Console.WriteLine("ไม่พบโมเดล PaddleOCR ภาษาไทย:");
                Console.WriteLine($"  - {ocrModelPath}");
                Console.WriteLine($"  - {ocrDictPath}");
                Console.ReadKey();
                return;
            }

            using var detector = new RtDetrDetector(modelPath);
            using var ocr = new PaddleOcrRecognizer(ocrModelPath, ocrDictPath);

            Environment.SetEnvironmentVariable(
                "OPENCV_FFMPEG_CAPTURE_OPTIONS",
                "rtsp_transport;tcp|fflags;nobuffer|flags;low_delay|max_delay;0");

            // สลับบรรทัดด้านล่างเพื่อเปลี่ยนระหว่าง webcam กับ RTSP
            //string rtspUrl = "rtsp://admin:Admin1234@192.168.11.80:554/Streaming/Channels/101";
            //using var capture = new VideoCapture(rtspUrl);
            using var capture = new VideoCapture(0);
            capture.Set(VideoCaptureProperties.BufferSize, 1);

            if (!capture.IsOpened())
            {
                Console.WriteLine("ไม่สามารถเปิดกล้องได้ ตรวจสอบว่ากล้องไม่ได้ถูกโปรแกรมอื่นใช้งานอยู่");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("เปิดกล้องสำเร็จ");
            Console.WriteLine("RT-DETR = หาป้าย  |  PaddleOCR = อ่านตัวอักษรบนป้าย");
            Console.WriteLine("ESC = ออก  |  [ = ลดความคมชัด  |  ] = เพิ่มความคมชัด");

            string windowName = "License Plate Detection - RT-DETRv2";
            Cv2.NamedWindow(windowName, WindowFlags.Normal);

            // Thread 1: อ่านเฟรมจากกล้องต่อเนื่อง ป้อนทั้ง display และ detector
            var grabberThread = new Thread(() => GrabberLoop(capture))
            {
                IsBackground = true,
                Name = "FrameGrabber"
            };
            grabberThread.Start();

            // Thread 2: inference แยกออกจาก display ทำงานคู่ขนานกัน
            var detectorThread = new Thread(() => DetectorLoop(detector, ocr))
            {
                IsBackground = true,
                Name = "Detector"
            };
            detectorThread.Start();

            // ── Main thread: Display loop วิ่งที่ FPS กล้อง ไม่รอ inference ──
            var fpsTimer = Stopwatch.StartNew();
            int fpsFrameCount = 0;
            float displayedFps = 0f;

            using var showFrame = new Mat();

            while (true)
            {
                bool gotFrame;

                lock (_displayLock)
                {
                    gotFrame = _hasDisplayFrame && _displayFrame != null && !_displayFrame.Empty();
                    if (gotFrame)
                    {
                        _displayFrame!.CopyTo(showFrame);
                        _hasDisplayFrame = false;
                    }
                }

                if (!gotFrame)
                {
                    if (Cv2.WaitKey(1) == 27) break;
                    continue;
                }

                // ── Sharpness ────────────────────────────────────────────────
                ApplySharpnessInPlace(showFrame, _sharpnessLevel);

                // ── วาดกล่องจากผลการตรวจจับล่าสุด ────────────────────────────
                // ใช้ lock สั้นๆ แค่ copy list ออกมา ไม่วาดภายใน lock เพื่อลด contention
                List<Detection> dets;
                List<PlateOcrPreview> previews;
                lock (_resultsLock)
                {
                    dets = new List<Detection>(_lastDetections);
                    previews = new List<PlateOcrPreview>(_lastOcrPreviews.Count);
                    foreach (var p in _lastOcrPreviews)
                    {
                        if (p.Image.IsDisposed || p.Image.Empty())
                            continue;

                        previews.Add(new PlateOcrPreview(
                            p.Image,
                            p.PlateNumber,
                            p.Province,
                            p.DetectConfidence));
                    }
                }

                foreach (var det in dets)
                {
                    Cv2.Rectangle(showFrame, det.Box, new Scalar(0, 255, 255), thickness: 2);

                    if (det.HasOcrResult)
                    {
                        int labelY = det.Box.Y - 8;
                        if (!string.IsNullOrWhiteSpace(det.PlateNumber))
                        {
                            var numSize = ThaiTextRenderer.MeasureText(det.PlateNumber, 18f);
                            labelY -= numSize.Height + 4;
                            ThaiTextRenderer.DrawText(showFrame, det.PlateNumber,
                                new Point(det.Box.X, labelY),
                                fontSize: 18f,
                                textColor: SD.Color.Black,
                                bgColor: SD.Color.FromArgb(255, 0, 255, 255));
                        }

                        if (!string.IsNullOrWhiteSpace(det.Province))
                        {
                            var provSize = ThaiTextRenderer.MeasureText(det.Province, 15f);
                            labelY -= provSize.Height + 4;
                            ThaiTextRenderer.DrawText(showFrame, det.Province,
                                new Point(det.Box.X, labelY),
                                fontSize: 15f,
                                textColor: SD.Color.Black,
                                bgColor: SD.Color.FromArgb(255, 200, 255, 255));
                        }
                    }
                    else
                    {
                        string fallback = $"plate {det.Confidence:F2}";
                        int baseline;
                        var textSize = Cv2.GetTextSize(fallback, HersheyFonts.HersheySimplex, 0.55, 1, out baseline);
                        var bgTL = new Point(det.Box.X, det.Box.Y - textSize.Height - 6);
                        var bgBR = new Point(det.Box.X + textSize.Width + 4, det.Box.Y);
                        Cv2.Rectangle(showFrame, bgTL, bgBR, new Scalar(0, 255, 255), thickness: -1);
                        Cv2.PutText(showFrame, fallback,
                            new Point(det.Box.X + 2, det.Box.Y - 4),
                            HersheyFonts.HersheySimplex, 0.55, new Scalar(0, 0, 0), thickness: 1);
                    }
                }

                // ── OSD overlay: FPS + ระดับความคมชัด ───────────────────────
                fpsFrameCount++;
                if (fpsTimer.Elapsed.TotalSeconds >= 1.0)
                {
                    displayedFps = (float)(fpsFrameCount / fpsTimer.Elapsed.TotalSeconds);
                    fpsFrameCount = 0;
                    fpsTimer.Restart();
                }

                string sharpText = _sharpnessLevel == 0 ? "off" : _sharpnessLevel.ToString();
                string osd = $"FPS: {displayedFps:F1}   Sharp: {sharpText}  ( [ ] )";

                // วาด outline ดำก่อน แล้วตามด้วยตัวอักษรเขียว ทำให้อ่านได้บนทุกสีพื้นหลัง
                Cv2.PutText(showFrame, osd, new Point(8, 24),
                    HersheyFonts.HersheySimplex, 0.55, new Scalar(0, 0, 0), thickness: 3);
                Cv2.PutText(showFrame, osd, new Point(8, 24),
                    HersheyFonts.HersheySimplex, 0.55, new Scalar(0, 230, 0), thickness: 1);

                using var composite = ComposeFrameWithOcrPanel(showFrame, previews);
                DisposeOcrPreviews(previews);
                Cv2.ImShow(windowName, composite);

                // WaitKey เรียกครั้งเดียวต่อเฟรม (ลดจาก 2 ครั้งในโค้ดเดิม)
                int key = Cv2.WaitKey(1);
                if (key == 27)
                {
                    Console.WriteLine("ESC กำลังปิดโปรแกรม...");
                    break;
                }
                // ASCII 91 = '[', 93 = ']'
                else if (key == 91 || key == 219) // '[' (US layout / Thai layout)
                    _sharpnessLevel = Math.Max(0, _sharpnessLevel - 1);
                else if (key == 93 || key == 221) // ']'
                    _sharpnessLevel = Math.Min(5, _sharpnessLevel + 1);
            }

            _running = false;
            grabberThread.Join(2000);
            detectorThread.Join(2000);

            _displayFrame?.Dispose();
            _pendingFrame?.Dispose();
            DisposeOcrPreviews(_lastOcrPreviews);
            capture.Release();
            Cv2.DestroyAllWindows();
        }

        // ── Thread 1: Grabber ────────────────────────────────────────────────
        // ฟีดเฟรมให้ทั้ง display loop (ทุกเฟรม) และ detector (เฉพาะตอน detector ว่าง)
        private static void GrabberLoop(VideoCapture capture)
        {
            using var frame = new Mat();

            while (_running)
            {
                if (!capture.Read(frame) || frame.Empty())
                {
                    Thread.Sleep(10); // กล้องหลุด: ไม่ให้ loop กิน CPU 100%
                    continue;
                }

                // อัปเดต display frame ทุกเฟรมที่ได้มา
                lock (_displayLock)
                {
                    if (_displayFrame == null) _displayFrame = frame.Clone();
                    else frame.CopyTo(_displayFrame);
                    _hasDisplayFrame = true;
                }

                // ส่งให้ detector เฉพาะเมื่อ detector ว่าง (ไม่สะสม queue)
                lock (_pendingLock)
                {
                    if (!_hasPendingFrame)
                    {
                        if (_pendingFrame == null) _pendingFrame = frame.Clone();
                        else frame.CopyTo(_pendingFrame);
                        _hasPendingFrame = true;
                    }
                    // ถ้า _hasPendingFrame == true หมายถึง detector ยังไม่ว่าง → drop เฟรมนี้
                }
            }
        }

        // ── Thread 2: Detector ───────────────────────────────────────────────
        // รัน inference แบบ async ไม่บล็อก display loop
        private static readonly PlateReadTracker _plateTracker = new();

        private static void DetectorLoop(RtDetrDetector detector, PaddleOcrRecognizer ocr)
        {
            using var detectionFrame = new Mat();

            while (_running)
            {
                bool hasFrame;

                lock (_pendingLock)
                {
                    hasFrame = _hasPendingFrame && _pendingFrame != null && !_pendingFrame.Empty();
                    if (hasFrame)
                    {
                        _pendingFrame!.CopyTo(detectionFrame);
                        _hasPendingFrame = false; // ปลด lock ให้ grabber ส่งเฟรมใหม่มาได้
                    }
                }

                if (!hasFrame)
                {
                    Thread.Sleep(1); // รอสั้นๆ โดยไม่กิน CPU
                    continue;
                }

                var results = detector.Detect(detectionFrame);
                var activeBoxes = new List<Rect>(results.Count);
                var newPreviews = new List<PlateOcrPreview>();

                foreach (var det in results)
                {
                    var (ocrResult, previewImage) = ocr.RecognizeThaiPlateFromFrameWithPreview(
                        detectionFrame, det.Box);

                    var tracked = _plateTracker.Update(
                        det.Box, ocrResult.PlateNumber, ocrResult.Province, det.Confidence);

                    det.PlateNumber = tracked.DisplayPlate;
                    det.Province = tracked.DisplayProvince;
                    det.PlateText = BuildPlateText(tracked.DisplayPlate, tracked.DisplayProvince);
                    activeBoxes.Add(det.Box);

                    if (previewImage != null && !previewImage.Empty())
                    {
                        newPreviews.Add(new PlateOcrPreview(
                            previewImage,
                            det.PlateNumber,
                            det.Province,
                            det.Confidence));
                        previewImage.Dispose();
                    }

                    if (tracked.ShouldLog)
                    {
                        Console.WriteLine(
                            $"[OCR] เลข: {tracked.LogPlate}  |  จังหวัด: {tracked.LogProvince}  " +
                            $"(detect: {tracked.LogConfidence:F2}, confirm: {tracked.ConfirmVotes}/{tracked.SampleCount})");
                    }
                }

                _plateTracker.MarkMissed(activeBoxes);

                lock (_resultsLock)
                {
                    _lastDetections = results;
                    DisposeOcrPreviews(_lastOcrPreviews);
                    _lastOcrPreviews = newPreviews;
                }
            }
        }

        private static Mat ComposeFrameWithOcrPanel(Mat videoFrame, IReadOnlyList<PlateOcrPreview> previews)
        {
            var composite = videoFrame.Clone();
            if (previews.Count == 0)
                return composite;

            int count = Math.Min(previews.Count, MaxPreviewOverlay);
            int rowH = PreviewThumbMaxH + 6;
            int textW = 150;
            int overlayW = PreviewMargin + PreviewThumbMaxW + textW + PreviewMargin;
            int overlayH = PreviewMargin + 14 + count * rowH + PreviewMargin;

            int x0 = PreviewMargin;
            int y0 = Math.Max(0, composite.Rows - overlayH - PreviewMargin);

            DrawSemiTransparentRect(composite, new Rect(x0, y0, overlayW, overlayH), 0.70);

            Cv2.PutText(composite, "OCR",
                new Point(x0 + 6, y0 + 12),
                HersheyFonts.HersheySimplex, 0.40, new Scalar(200, 200, 200), 1);

            int y = y0 + 18;
            for (int i = 0; i < count; i++)
            {
                var preview = previews[i];
                if (preview.Image.IsDisposed || preview.Image.Empty())
                    continue;

                using var previewCopy = preview.Image.Clone();
                double scale = Math.Min(PreviewThumbMaxW / (double)previewCopy.Width,
                    PreviewThumbMaxH / (double)previewCopy.Height);
                int thumbW = Math.Max(1, (int)(previewCopy.Width * scale));
                int thumbH = Math.Max(1, (int)(previewCopy.Height * scale));

                using var thumb = new Mat();
                Cv2.Resize(previewCopy, thumb, new Size(thumbW, thumbH), interpolation: InterpolationFlags.Linear);

                int thumbX = x0 + PreviewMargin;
                int thumbY = y + (PreviewThumbMaxH - thumbH) / 2;
                thumb.CopyTo(new Mat(composite, new Rect(thumbX, thumbY, thumbW, thumbH)));
                Cv2.Rectangle(composite,
                    new Point(thumbX - 1, thumbY - 1),
                    new Point(thumbX + thumbW, thumbY + thumbH),
                    new Scalar(0, 255, 255), 1);

                int textX = thumbX + PreviewThumbMaxW + 6;
                int textY = y + 2;

                if (!string.IsNullOrWhiteSpace(preview.PlateNumber))
                {
                    ThaiTextRenderer.DrawText(composite, preview.PlateNumber,
                        new Point(textX, textY),
                        fontSize: 11f,
                        textColor: SD.Color.Black,
                        bgColor: SD.Color.FromArgb(200, 0, 255, 255));
                    textY += 16;
                }

                if (!string.IsNullOrWhiteSpace(preview.Province))
                {
                    ThaiTextRenderer.DrawText(composite, preview.Province,
                        new Point(textX, textY),
                        fontSize: 10f,
                        textColor: SD.Color.Black,
                        bgColor: SD.Color.FromArgb(200, 200, 255, 255));
                    textY += 14;
                }

                Cv2.PutText(composite, $"{preview.DetectConfidence:F2}",
                    new Point(textX, textY + 10),
                    HersheyFonts.HersheySimplex, 0.35, new Scalar(160, 220, 160), 1);

                y += rowH;
            }

            return composite;
        }

        private static void DrawSemiTransparentRect(Mat frame, Rect rect, double alpha)
        {
            rect.X = Math.Clamp(rect.X, 0, Math.Max(0, frame.Cols - 1));
            rect.Y = Math.Clamp(rect.Y, 0, Math.Max(0, frame.Rows - 1));
            rect.Width = Math.Min(rect.Width, frame.Cols - rect.X);
            rect.Height = Math.Min(rect.Height, frame.Rows - rect.Y);
            if (rect.Width <= 0 || rect.Height <= 0)
                return;

            using var roi = new Mat(frame, rect);
            using var tint = new Mat(roi.Size(), roi.Type(), new Scalar(24, 24, 24));
            Cv2.AddWeighted(tint, alpha, roi, 1.0 - alpha, 0, roi);
        }

        private static void DisposeOcrPreviews(List<PlateOcrPreview> previews)
        {
            foreach (var preview in previews)
                preview.Dispose();
            previews.Clear();
        }

        private static string BuildPlateText(string plateNumber, string province)
        {
            if (string.IsNullOrWhiteSpace(plateNumber) && string.IsNullOrWhiteSpace(province))
                return string.Empty;
            if (string.IsNullOrWhiteSpace(province))
                return plateNumber.Trim();
            if (string.IsNullOrWhiteSpace(plateNumber))
                return province.Trim();
            return $"{plateNumber.Trim()} | {province.Trim()}";
        }

        // ── Unsharp mask ─────────────────────────────────────────────────────
        // level 0 = ข้ามเลย, level 1–5 = ความคมชัดเพิ่มขึ้น
        // หลักการ: sharpen = original + amount × (original − blurred)
        //        = original × (1+a) + blurred × (−a)
        // sigma ใหญ่ขึ้น = radius ที่ใช้หาขอบกว้างขึ้น → ผลคมชัดชัดเจนกว่า
        private static void ApplySharpnessInPlace(Mat frame, int level)
        {
            if (level == 0) return;

            using var blurred = new Mat();
            double sigma  = 1.0 + level * 0.5; // 1.5 – 3.5 ตามระดับ 1–5
            double amount = level * 0.4;        // 0.4 – 2.0

            Cv2.GaussianBlur(frame, blurred, new Size(0, 0), sigmaX: sigma);
            Cv2.AddWeighted(frame, 1.0 + amount, blurred, -amount, gamma: 0, dst: frame);
        }
    }
}
