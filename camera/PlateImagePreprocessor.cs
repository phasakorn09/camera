using System;
using OpenCvSharp;

namespace ConsoleApp1
{
    /// <summary>เตรียมภาพป้ายก่อน detect / OCR</summary>
    internal static class PlateImagePreprocessor
    {
        private const int MinPlateWidth = 200;
        private const int TargetPlateWidth = 320;
        private const double FallbackTopLineRatio = 0.55;
        private const double BlurVarianceThreshold = 18.0;

        /// <summary>วัดความเบลอ — ค่าต่ำ = เบลอ (Laplacian variance)</summary>
        public static bool IsBlurry(Mat bgr)
        {
            if (bgr.Empty() || bgr.Width < 8 || bgr.Height < 8)
                return true;

            using var gray = new Mat();
            Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);
            using var laplacian = new Mat();
            Cv2.Laplacian(gray, laplacian, MatType.CV_64F);
            Cv2.MeanStdDev(laplacian, out _, out Scalar stddev);
            return stddev.Val0 < BlurVarianceThreshold;
        }

        /// <summary>sharpen เบา — ใช้ retry OCR เมื่อภาพไม่ชัด</summary>
        public static Mat SharpenForOcr(Mat bgr, double amount = 0.55)
        {
            var result = bgr.Clone();
            using var blurred = new Mat();
            Cv2.GaussianBlur(result, blurred, new Size(0, 0), 2.5);
            Cv2.AddWeighted(result, 1.0 + amount, blurred, -amount, 0, result);
            return result;
        }

        /// <summary>ลด noise รักษาขอบ — เหมาะกับป้ายเบลอ/แสงน้อย</summary>
        public static Mat DenoiseForOcr(Mat bgr)
        {
            var result = new Mat();
            Cv2.BilateralFilter(bgr, result, d: 5, sigmaColor: 45, sigmaSpace: 45);
            return result;
        }

        private static void EnhanceBlurryPlate(Mat work)
        {
            if (work.Empty())
                return;

            bool blurry = IsBlurry(work);
            if (!blurry && work.Width >= MinPlateWidth)
                return;

            using (var denoised = DenoiseForOcr(work))
                denoised.CopyTo(work);

            if (blurry && work.Width < TargetPlateWidth)
            {
                double scale = Math.Min(1.6, TargetPlateWidth / (double)work.Width);
                if (scale > 1.05)
                {
                    Cv2.Resize(work, work, new Size(), scale, scale, InterpolationFlags.Cubic);
                }
            }

            if (blurry)
            {
                using var sharp = SharpenForOcr(work);
                sharp.CopyTo(work);
                using var enhanced = ApplyClaheBgr(work, clipLimit: 2.4);
                enhanced.CopyTo(work);
            }
        }

        /// <summary>CLAHE บน BGR — ใช้ก่อน detect เพื่อให้ขอบป้ายสีๆ ชัดขึ้น</summary>
        public static Mat ApplyClaheBgr(Mat bgr, double clipLimit = 2.0)
        {
            var result = bgr.Clone();
            using var lab = new Mat();
            Cv2.CvtColor(result, lab, ColorConversionCodes.BGR2Lab);

            var channels = Cv2.Split(lab);
            try
            {
                using var clahe = Cv2.CreateCLAHE(clipLimit, new Size(8, 8));
                clahe.Apply(channels[0], channels[0]);
                Cv2.Merge(channels, lab);
                Cv2.CvtColor(lab, result, ColorConversionCodes.Lab2BGR);
            }
            finally
            {
                foreach (var ch in channels)
                    ch.Dispose();
            }

            return result;
        }

        /// <summary>Crop ป้ายจากเฟรม — padding ตามสัดส่วนกล่อง</summary>
        public static Mat CropPlateForOcr(Mat frame, Rect box)
        {
            int padX = Math.Clamp(box.Width / 8, 8, 24);
            int padY = Math.Clamp(box.Height / 5, 6, 18);

            int x = Math.Max(0, box.X - padX);
            int y = Math.Max(0, box.Y - padY);
            int w = Math.Min(frame.Width - x, box.Width + padX * 2);
            int h = Math.Min(frame.Height - y, box.Height + padY * 2);

            if (w <= 0 || h <= 0)
                return new Mat();

            return new Mat(frame, new Rect(x, y, w, h)).Clone();
        }

        /// <summary>Upscale → CLAHE เบา → deskew → ตัดขอบดำ ก่อนแยกบรรทัด OCR</summary>
        public static Mat PreparePlateCropForOcr(Mat cropBgr)
        {
            if (cropBgr.Empty() || cropBgr.Width < 8 || cropBgr.Height < 8)
                return cropBgr.Clone();

            var work = cropBgr.Clone();

            if (work.Width < MinPlateWidth)
            {
                double scale = TargetPlateWidth / (double)work.Width;
                scale = Math.Min(scale, 3.0);
                Cv2.Resize(
                    work, work, new Size(),
                    scale, scale,
                    InterpolationFlags.Cubic);
            }

            using (var enhanced = ApplyClaheBgr(work, clipLimit: 1.8))
            {
                enhanced.CopyTo(work);
            }

            using var deskewed = Deskew(work);
            deskewed.CopyTo(work);

            using var trimmed = TrimContentBorder(work);
            trimmed.CopyTo(work);

            EnhanceBlurryPlate(work);

            return work;
        }

        /// <summary>แยกบรรทัดบน/ล่างจาก projection แนวนอน (ช่องว่างระหว่าง 2 บรรทัด)</summary>
        public static void SplitPlateLines(Mat plateBgr, out Mat topLine, out Mat bottomLine, out int splitY)
        {
            splitY = FindLineSplitY(plateBgr);
            splitY = Math.Clamp(splitY, plateBgr.Height / 4, plateBgr.Height * 3 / 4);

            topLine = new Mat(plateBgr, new Rect(0, 0, plateBgr.Width, splitY)).Clone();
            bottomLine = new Mat(plateBgr,
                new Rect(0, splitY, plateBgr.Width, plateBgr.Height - splitY)).Clone();
        }

        /// <summary>หมุนป้ายให้แนวนอนตาม minAreaRect ก่อนส่ง OCR</summary>
        public static Mat Deskew(Mat bgr)
        {
            if (bgr.Empty() || bgr.Width < 8 || bgr.Height < 8)
                return bgr.Clone();

            using var gray = new Mat();
            Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);
            using var blurred = new Mat();
            Cv2.GaussianBlur(gray, blurred, new Size(3, 3), 0);
            using var binary = new Mat();
            Cv2.Threshold(blurred, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

            using var locations = new Mat();
            Cv2.FindNonZero(binary, locations);
            if (locations.Empty() || locations.Rows < 10)
                return bgr.Clone();

            int rows = locations.Rows;
            var points = new Point[rows];
            for (int i = 0; i < rows; i++)
                points[i] = locations.At<Point>(i);

            RotatedRect box = Cv2.MinAreaRect(points);
            float angle = box.Angle;

            if (box.Size.Width < box.Size.Height)
                angle += 90f;

            if (Math.Abs(angle) < 0.5f || Math.Abs(angle) > 30f)
                return bgr.Clone();

            var center = new Point2f(bgr.Cols / 2f, bgr.Rows / 2f);
            using var rotMat = Cv2.GetRotationMatrix2D(center, angle, 1.0);
            var rotated = new Mat();
            Cv2.WarpAffine(
                bgr, rotated, rotMat, bgr.Size(),
                InterpolationFlags.Cubic,
                BorderTypes.Replicate);

            return rotated;
        }

        /// <summary>ตัดขอบดำ/พื้นหลังว่างรอบป้าย</summary>
        private static Mat TrimContentBorder(Mat bgr, int margin = 2)
        {
            if (bgr.Empty())
                return bgr.Clone();

            using var gray = new Mat();
            Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);
            using var binary = new Mat();
            Cv2.Threshold(gray, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

            // Otsu บนป้าย: ตัวอักษรมักเข้ม — ใช้ pixel ที่ไม่ใช่พื้นหลังสว่างสุด
            using var ink = new Mat();
            Cv2.Threshold(gray, ink, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);

            using var locations = new Mat();
            Cv2.FindNonZero(ink, locations);
            if (locations.Empty() || locations.Rows < 20)
                return bgr.Clone();

            var points = new Point[locations.Rows];
            for (int i = 0; i < locations.Rows; i++)
                points[i] = locations.At<Point>(i);

            Rect bounds = Cv2.BoundingRect(points);
            bounds.X = Math.Max(0, bounds.X - margin);
            bounds.Y = Math.Max(0, bounds.Y - margin);
            bounds.Width = Math.Min(bgr.Width - bounds.X, bounds.Width + margin * 2);
            bounds.Height = Math.Min(bgr.Height - bounds.Y, bounds.Height + margin * 2);

            if (bounds.Width < 8 || bounds.Height < 8)
                return bgr.Clone();

            return new Mat(bgr, bounds).Clone();
        }

        private static int FindLineSplitY(Mat bgr)
        {
            int h = bgr.Rows;
            if (h < 12)
                return Math.Max(1, h / 2);

            using var gray = new Mat();
            Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);

            var rowInk = new int[h];
            for (int y = 0; y < h; y++)
            {
                int sum = 0;
                for (int x = 0; x < gray.Cols; x++)
                {
                    if (gray.At<byte>(y, x) < 140)
                        sum++;
                }

                rowInk[y] = sum;
            }

            var smoothed = new int[h];
            for (int y = 0; y < h; y++)
            {
                int a = rowInk[Math.Max(0, y - 1)];
                int b = rowInk[y];
                int c = rowInk[Math.Min(h - 1, y + 1)];
                smoothed[y] = (a + b + c) / 3;
            }

            int searchStart = h * 35 / 100;
            int searchEnd = h * 72 / 100;
            if (searchEnd <= searchStart + 2)
                return (int)(h * FallbackTopLineRatio);

            int minInk = int.MaxValue;
            int splitY = h / 2;
            for (int y = searchStart; y < searchEnd; y++)
            {
                if (smoothed[y] < minInk)
                {
                    minInk = smoothed[y];
                    splitY = y;
                }
            }

            int topRegion = 0;
            for (int y = 0; y < h / 3; y++)
                topRegion += smoothed[y];
            topRegion /= Math.Max(1, h / 3);

            int bottomRegion = 0;
            for (int y = h * 2 / 3; y < h; y++)
                bottomRegion += smoothed[y];
            bottomRegion /= Math.Max(1, h / 3);

            int midAvg = 0;
            for (int y = searchStart; y < searchEnd; y++)
                midAvg += smoothed[y];
            midAvg /= Math.Max(1, searchEnd - searchStart);

            // ช่องว่างจริงต้องมี ink น้อยกว่าบรรทัดบน/ล่างชัดเจน
            if (minInk > midAvg * 0.85 || minInk > topRegion * 0.55 || minInk > bottomRegion * 0.55)
                return (int)(h * FallbackTopLineRatio);

            return splitY;
        }
    }
}
