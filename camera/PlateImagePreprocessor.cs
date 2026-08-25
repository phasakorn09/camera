using System;
using System.Collections.Generic;
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

        /// <summary>คะแนนความคมชัด (Laplacian variance) — สูง = ชัด</summary>
        public static double MeasureSharpnessScore(Mat bgr)
        {
            if (bgr.Empty() || bgr.Width < 8 || bgr.Height < 8)
                return 0;

            using var gray = new Mat();
            Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);
            using var laplacian = new Mat();
            Cv2.Laplacian(gray, laplacian, MatType.CV_64F);
            Cv2.MeanStdDev(laplacian, out _, out Scalar stddev);
            return stddev.Val0;
        }

        /// <summary>วัดความเบลอ — ค่าต่ำ = เบลอ (Laplacian variance)</summary>
        public static bool IsBlurry(Mat bgr) =>
            MeasureSharpnessScore(bgr) < BlurVarianceThreshold;

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

        private static void EnhancePlateClarity(Mat work)
        {
            if (work.Empty())
                return;

            bool blurry = IsBlurry(work);

            if (blurry)
            {
                using var denoised = DenoiseForOcr(work);
                denoised.CopyTo(work);
            }

            if (work.Width < TargetPlateWidth)
            {
                double scale = Math.Min(2.0, TargetPlateWidth / (double)work.Width);
                if (scale > 1.04)
                {
                    Cv2.Resize(work, work, new Size(), scale, scale, InterpolationFlags.Cubic);
                }
            }

            double sharpAmount = blurry ? 0.55 : 0.30;
            using (var sharp = SharpenForOcr(work, sharpAmount))
            {
                sharp.CopyTo(work);
            }

            double claheLimit = blurry ? 2.3 : 1.7;
            using (var enhanced = ApplyClaheBgr(work, claheLimit))
            {
                enhanced.CopyTo(work);
            }
        }

        /// <summary>หมุนป้ายให้แนวนอน — ใช้ Hough จากตัวอักษร + fallback minAreaRect</summary>
        public static Mat Deskew(Mat bgr) => StraightenPlate(bgr);

        private static Mat StraightenPlate(Mat bgr)
        {
            if (bgr.Empty() || bgr.Width < 8 || bgr.Height < 8)
                return bgr.Clone();

            using var gray = new Mat();
            Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);

            if (TryEstimateSkewAngle(gray, out float angle))
                return RotatePlate(bgr, angle);

            if (TryEstimateSkewFromInkRect(gray, out angle))
                return RotatePlate(bgr, angle);

            return bgr.Clone();
        }

        /// <summary>ประมาณมุมเอียงจากเส้นข้อความแนวนอน</summary>
        private static bool TryEstimateSkewAngle(Mat gray, out float angleDeg)
        {
            angleDeg = 0f;
            using var ink = BuildInkMask(gray);

            using var edges = new Mat();
            Cv2.Canny(ink, edges, 40, 120);

            LineSegmentPoint[] lines = Cv2.HoughLinesP(
                edges,
                rho: 1,
                theta: Math.PI / 180,
                threshold: Math.Max(12, gray.Width / 18),
                minLineLength: Math.Max(10, gray.Width / 8),
                maxLineGap: Math.Max(4, gray.Width / 40));

            if (lines.Length == 0)
                return false;

            var angles = new List<float>(lines.Length);
            foreach (LineSegmentPoint line in lines)
            {
                float dx = line.P2.X - line.P1.X;
                float dy = line.P2.Y - line.P1.Y;
                if (MathF.Abs(dx) < 6f)
                    continue;

                float angle = MathF.Atan2(dy, dx) * (180f / MathF.PI);
                if (MathF.Abs(angle) > 28f)
                    continue;

                angles.Add(angle);
            }

            if (angles.Count < 2)
                return false;

            angles.Sort();
            angleDeg = angles[angles.Count / 2];

            if (MathF.Abs(angleDeg) < 0.35f)
                return false;

            return true;
        }

        private static bool TryEstimateSkewFromInkRect(Mat gray, out float angleDeg)
        {
            angleDeg = 0f;
            using var ink = BuildInkMask(gray);

            using var locations = new Mat();
            Cv2.FindNonZero(ink, locations);
            if (locations.Empty() || locations.Rows < 20)
                return false;

            int rows = locations.Rows;
            var points = new Point[rows];
            for (int i = 0; i < rows; i++)
                points[i] = locations.At<Point>(i);

            RotatedRect box = Cv2.MinAreaRect(points);
            float angle = box.Angle;

            if (box.Size.Width < box.Size.Height)
                angle += 90f;

            if (MathF.Abs(angle) < 0.35f || MathF.Abs(angle) > 28f)
                return false;

            angleDeg = angle;
            return true;
        }

        private static Mat BuildInkMask(Mat gray)
        {
            var ink = new Mat();
            Cv2.GaussianBlur(gray, ink, new Size(3, 3), 0);
            Cv2.Threshold(ink, ink, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);

            using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
            Cv2.MorphologyEx(ink, ink, MorphTypes.Close, kernel, iterations: 1);
            return ink;
        }

        private static Mat RotatePlate(Mat bgr, float angleDeg)
        {
            if (MathF.Abs(angleDeg) < 0.35f)
                return bgr.Clone();

            var center = new Point2f(bgr.Width / 2f, bgr.Height / 2f);
            using var rotMat = Cv2.GetRotationMatrix2D(center, angleDeg, 1.0);

            double cos = Math.Abs(rotMat.At<double>(0, 0));
            double sin = Math.Abs(rotMat.At<double>(0, 1));
            int newW = (int)Math.Ceiling(bgr.Height * sin + bgr.Width * cos);
            int newH = (int)Math.Ceiling(bgr.Height * cos + bgr.Width * sin);

            rotMat.Set(0, 2, rotMat.At<double>(0, 2) + (newW - bgr.Width) / 2.0);
            rotMat.Set(1, 2, rotMat.At<double>(1, 2) + (newH - bgr.Height) / 2.0);

            var rotated = new Mat();
            Cv2.WarpAffine(
                bgr,
                rotated,
                rotMat,
                new Size(newW, newH),
                InterpolationFlags.Cubic,
                BorderTypes.Replicate);

            return rotated;
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

        /// <summary>Crop ป้ายจากเฟรม — padding เล็ก + จูนให้ชิดขอบตัวอักษร</summary>
        public static Mat CropPlateForOcr(Mat frame, Rect box)
        {
            int padX = Math.Clamp(box.Width / 32, 1, 4);
            int padY = Math.Clamp(box.Height / 24, 1, 3);

            Rect expanded = ExpandRect(box, padX, padY, frame.Width, frame.Height);
            if (expanded.Width <= 0 || expanded.Height <= 0)
                return new Mat();

            using var rough = new Mat(frame, expanded).Clone();
            return TightenCropToPlateEdges(rough, edgeMargin: 2);
        }

        private static Rect ExpandRect(Rect box, int padX, int padY, int frameW, int frameH)
        {
            int x = Math.Max(0, box.X - padX);
            int y = Math.Max(0, box.Y - padY);
            int w = Math.Min(frameW - x, box.Width + padX * 2);
            int h = Math.Min(frameH - y, box.Height + padY * 2);
            return new Rect(x, y, w, h);
        }

        /// <summary>Upscale → CLAHE → ตรงป้าย → ตัดขอบ → ทำให้ชัด ก่อนแยกบรรทัด OCR</summary>
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

            using (var preTrim = TightenCropToPlateEdges(work, edgeMargin: 1))
            {
                preTrim.CopyTo(work);
            }

            using (var straightened = StraightenPlate(work))
            {
                straightened.CopyTo(work);
            }

            using (var trimmed = TightenCropToPlateEdges(work, edgeMargin: 2))
            {
                trimmed.CopyTo(work);
            }

            EnhancePlateClarity(work);

            return work;
        }

        /// <summary>crop ซ้ายบรรทัดบน — โฟกัสพยัญชนะ 1–2 ตัว + upscale/CLAHE</summary>
        public static Mat ExtractPlatePrefixStrip(Mat topLineBgr)
        {
            if (topLineBgr.Empty() || topLineBgr.Width < 8 || topLineBgr.Height < 4)
                return topLineBgr.Clone();

            int cropW = Math.Clamp(topLineBgr.Width * 44 / 100, Math.Min(64, topLineBgr.Width), topLineBgr.Width);
            int padY = Math.Max(2, topLineBgr.Height / 10);

            using var strip = new Mat(topLineBgr, new Rect(0, 0, cropW, topLineBgr.Height)).Clone();
            var padded = new Mat();
            Cv2.CopyMakeBorder(strip, padded, padY, padY, 10, 4, BorderTypes.Replicate);
            return EnhancePrefixStripForOcr(padded);
        }

        /// <summary>เตรียมโซน prefix — upscale + CLAHE + sharpen เบา</summary>
        private static Mat EnhancePrefixStripForOcr(Mat bgr)
        {
            Mat work = bgr.Clone();

            if (work.Width < 160)
            {
                var upscaled = new Mat();
                Cv2.Resize(work, upscaled,
                    new Size(work.Width * 2, work.Height * 2),
                    0, 0,
                    InterpolationFlags.Cubic);
                work.Dispose();
                work = upscaled;
            }

            using (var sharp = SharpenForOcr(work, amount: 0.45))
            {
                sharp.CopyTo(work);
            }

            using (var enhanced = ApplyClaheBgr(work, clipLimit: 2.2))
            {
                enhanced.CopyTo(work);
            }

            return work;
        }

        /// <summary>เตรียมบรรทัดจังหวัด — upscale + CLAHE + sharpen สำหรับ retry OCR</summary>
        public static Mat EnhanceProvinceLineForOcr(Mat bottomLineBgr)
        {
            Mat work = bottomLineBgr.Clone();

            if (work.Width < 180)
            {
                var upscaled = new Mat();
                Cv2.Resize(work, upscaled,
                    new Size(work.Width * 2, work.Height * 2),
                    0, 0,
                    InterpolationFlags.Cubic);
                work.Dispose();
                work = upscaled;
            }

            using (var sharp = SharpenForOcr(work, amount: 0.5))
            {
                sharp.CopyTo(work);
            }

            using (var enhanced = ApplyClaheBgr(work, clipLimit: 2.0))
            {
                enhanced.CopyTo(work);
            }

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

        /// <summary>ตัดขอบว่าง/พื้นหลัง — ใช้ projection ของตัวอักษรให้ชิดขอบป้าย</summary>
        private static Mat TightenCropToPlateEdges(Mat bgr, int edgeMargin = 2)
        {
            if (bgr.Empty() || bgr.Width < 8 || bgr.Height < 8)
                return bgr.Clone();

            using var gray = new Mat();
            Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);

            using var ink = new Mat();
            Cv2.Threshold(gray, ink, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);

            using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
            using var cleaned = new Mat();
            Cv2.MorphologyEx(ink, cleaned, MorphTypes.Open, kernel, iterations: 1);
            Cv2.Dilate(cleaned, cleaned, kernel, iterations: 1);

            if (TryFindInkBoundsByProjection(cleaned, out Rect bounds))
            {
                bounds = ExpandRect(bounds, edgeMargin, edgeMargin, bgr.Width, bgr.Height);
                if (IsReasonableTightBounds(bgr.Width, bgr.Height, bounds))
                    return new Mat(bgr, bounds).Clone();
            }

            return TightenCropByInkBoundingRect(bgr, gray, edgeMargin);
        }

        /// <summary>หาขอบจากความหนาแน่นตัวอักษรแต่ละแถว/คอลัมน์</summary>
        private static bool TryFindInkBoundsByProjection(Mat inkMask, out Rect bounds)
        {
            bounds = default;
            int w = inkMask.Cols;
            int h = inkMask.Rows;

            var colInk = new int[w];
            var rowInk = new int[h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (inkMask.At<byte>(y, x) == 0)
                        continue;

                    colInk[x]++;
                    rowInk[y]++;
                }
            }

            int maxCol = 0;
            int maxRow = 0;
            for (int x = 0; x < w; x++)
                maxCol = Math.Max(maxCol, colInk[x]);
            for (int y = 0; y < h; y++)
                maxRow = Math.Max(maxRow, rowInk[y]);

            if (maxCol < 2 || maxRow < 2)
                return false;

            int colThreshold = Math.Max(2, maxCol * 6 / 100);
            int rowThreshold = Math.Max(2, maxRow * 6 / 100);

            int left = -1;
            int right = -1;
            int top = -1;
            int bottom = -1;

            for (int x = 0; x < w; x++)
            {
                if (colInk[x] >= colThreshold)
                {
                    left = x;
                    break;
                }
            }

            for (int x = w - 1; x >= 0; x--)
            {
                if (colInk[x] >= colThreshold)
                {
                    right = x;
                    break;
                }
            }

            for (int y = 0; y < h; y++)
            {
                if (rowInk[y] >= rowThreshold)
                {
                    top = y;
                    break;
                }
            }

            for (int y = h - 1; y >= 0; y--)
            {
                if (rowInk[y] >= rowThreshold)
                {
                    bottom = y;
                    break;
                }
            }

            if (left < 0 || right < 0 || top < 0 || bottom < 0 || right <= left || bottom <= top)
                return false;

            bounds = new Rect(left, top, right - left + 1, bottom - top + 1);
            return true;
        }

        private static bool IsReasonableTightBounds(int srcW, int srcH, Rect bounds)
        {
            if (bounds.Width < 8 || bounds.Height < 8)
                return false;

            // กัน crop มากเกินไปเมื่อ projection พลาด
            return bounds.Width >= srcW * 45 / 100
                && bounds.Height >= srcH * 40 / 100
                && bounds.Width <= srcW
                && bounds.Height <= srcH;
        }

        /// <summary>fallback — bounding rect ของ pixel ตัวอักษร</summary>
        private static Mat TightenCropByInkBoundingRect(Mat bgr, Mat gray, int margin)
        {
            using var ink = new Mat();
            Cv2.Threshold(gray, ink, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);

            using var locations = new Mat();
            Cv2.FindNonZero(ink, locations);
            if (locations.Empty() || locations.Rows < 20)
                return bgr.Clone();

            int rows = locations.Rows;
            var points = new Point[rows];
            for (int i = 0; i < rows; i++)
                points[i] = locations.At<Point>(i);

            Rect bounds = Cv2.BoundingRect(points);
            bounds = ExpandRect(bounds, margin, margin, bgr.Width, bgr.Height);

            if (!IsReasonableTightBounds(bgr.Width, bgr.Height, bounds))
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
