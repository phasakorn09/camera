using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ConsoleApp1
{
    internal readonly record struct ThaiPlateVisionReading(
        string PlateNumber,
        string Letters,
        string Digits,
        string Province,
        string Confidence,
        bool CanReport);

    /// <summary>
    /// รายงานผลตามสเปก Vision: แยกตัวอักษร / หมายเลข / จังหวัด + ความมั่นใจ
    /// ไม่ส่งทะเบียนที่ตรวจรูปแบบไม่ผ่าน (ไม่เดา)
    /// </summary>
    internal static class ThaiPlateVisionReport
    {
        public const string Unreadable = "อ่านไม่ได้";
        public const string VehicleNoPlate = "พบรถ แต่ไม่สามารถอ่านป้ายทะเบียนได้";
        public const string NoVehicle = "ไม่พบรถในภาพ";

        public static ThaiPlateVisionReading FromCapture(in PlateCaptureResult capture)
        {
            ThaiPlateParts.Split(capture.PlateNumber, out string letters, out string digits);
            string confidence = LabelConfidence(
                capture.IsValidated,
                capture.DetectConfidence,
                capture.SharpnessScore,
                capture.ReadQualityScore);

            if (!capture.IsValidated)
            {
                return new ThaiPlateVisionReading(
                    Unreadable,
                    Unreadable,
                    Unreadable,
                    string.IsNullOrWhiteSpace(capture.Province) ? Unreadable : capture.Province,
                    "ต่ำ",
                    CanReport: false);
            }

            return new ThaiPlateVisionReading(
                string.IsNullOrWhiteSpace(digits) ? Unreadable : digits,
                string.IsNullOrWhiteSpace(letters) ? Unreadable : letters,
                string.IsNullOrWhiteSpace(digits) ? Unreadable : digits,
                string.IsNullOrWhiteSpace(capture.Province) ? Unreadable : capture.Province,
                confidence,
                CanReport: true);
        }

        public static string LabelConfidence(
            bool isValidated,
            float detectConfidence,
            double sharpness,
            float readQualityScore)
        {
            if (!isValidated)
                return "ต่ำ";

            if (sharpness >= 40.0 && detectConfidence >= 0.15f && readQualityScore >= 0.4f)
                return "สูง";

            if (sharpness >= 25.0 && detectConfidence >= 0.10f)
                return "ปานกลาง";

            return "ต่ำ";
        }

        public static string FormatReading(in ThaiPlateVisionReading reading, int index)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"คันที่ {index}");
            sb.AppendLine($"  หมายเลขทะเบียน: {reading.Digits}");
            sb.AppendLine($"  ตัวอักษรบนทะเบียน: {reading.Letters}");
            sb.AppendLine($"  จังหวัด: {reading.Province}");
            sb.Append($"  ความมั่นใจ: {reading.Confidence}");
            return sb.ToString();
        }

        public static string FormatSummary(
            IReadOnlyList<ThaiPlateVisionReading> readings,
            bool sawPlateDetection)
        {
            var reportable = readings.Where(r => r.CanReport).ToList();
            if (reportable.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine("=== ผลการอ่านป้ายทะเบียน ===");
                for (int i = 0; i < reportable.Count; i++)
                {
                    if (i > 0)
                        sb.AppendLine();
                    sb.Append(FormatReading(reportable[i], i + 1));
                }
                return sb.ToString();
            }

            if (sawPlateDetection || readings.Count > 0)
                return VehicleNoPlate;

            return NoVehicle;
        }
    }
}
