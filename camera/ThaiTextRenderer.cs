using System;
using System.Drawing;
using System.Drawing.Text;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace ConsoleApp1
{
    /// <summary>วาดข้อความภาษาไทยลง Mat (OpenCV PutText ไม่รองรับไทย)</summary>
    internal static class ThaiTextRenderer
    {
        private static readonly string[] FontCandidates =
        {
            "Leelawadee UI", "Tahoma", "Angsana New", "Microsoft Sans Serif"
        };

        public static void DrawText(Mat target, string text, OpenCvSharp.Point origin, float fontSize, Color textColor, Color? bgColor = null)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            using var bitmap = BitmapConverter.ToBitmap(target);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            using var font = CreateThaiFont(fontSize);
            var textSize = graphics.MeasureString(text, font);

            int x = origin.X;
            int y = origin.Y;
            int pad = 3;

            if (bgColor.HasValue)
            {
                using var bgBrush = new SolidBrush(bgColor.Value);
                graphics.FillRectangle(bgBrush,
                    x - pad, y - pad,
                    textSize.Width + pad * 2, textSize.Height + pad * 2);
            }

            using var brush = new SolidBrush(textColor);
            graphics.DrawString(text, font, brush, x, y);

            BitmapConverter.ToMat(bitmap, target);
        }

        public static System.Drawing.Size MeasureText(string text, float fontSize)
        {
            using var bmp = new Bitmap(1, 1);
            using var g = Graphics.FromImage(bmp);
            using var font = CreateThaiFont(fontSize);
            var size = g.MeasureString(text, font);
            return new System.Drawing.Size((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height));
        }

        private static Font CreateThaiFont(float fontSize)
        {
            foreach (var name in FontCandidates)
            {
                try { return new Font(name, fontSize, FontStyle.Bold, GraphicsUnit.Pixel); }
                catch { /* ลองฟอนต์ถัดไป */ }
            }
            return new Font(SystemFonts.DefaultFont.FontFamily, fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        }
    }
}
