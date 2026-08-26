using System;

namespace ConsoleApp1
{
    /// <summary>
    /// แยกป้ายไทยที่ normalize แล้วเป็นตัวอักษร (รวมเลขหมวดถ้ามี) กับหมายเลข
    /// </summary>
    internal static class ThaiPlateParts
    {
        public static void Split(string plateNumber, out string letters, out string digits)
        {
            letters = string.Empty;
            digits = string.Empty;

            if (string.IsNullOrWhiteSpace(plateNumber))
                return;

            string normalized = plateNumber.Trim();
            int space = normalized.IndexOf(' ');
            if (space > 0)
            {
                letters = normalized[..space].Trim();
                digits = normalized[(space + 1)..].Trim();
                return;
            }

            int firstDigitRun = FindTrailingDigitStart(normalized);
            if (firstDigitRun < 0)
            {
                letters = normalized;
                return;
            }

            letters = normalized[..firstDigitRun].Trim();
            digits = normalized[firstDigitRun..].Trim();
        }

        /// <summary>
        /// หาจุดเริ่มของเลขทะเบียนท้ายป้าย โดยไม่ตัดเลขหมวดหน้าพยัญชนะ เช่น 1กข1234 → 1234
        /// </summary>
        private static int FindTrailingDigitStart(string plate)
        {
            int lastDigit = -1;
            for (int i = plate.Length - 1; i >= 0; i--)
            {
                if (char.IsDigit(plate[i]))
                    lastDigit = i;
                else
                    break;
            }

            return lastDigit;
        }
    }
}
