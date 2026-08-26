using System;
using System.Linq;

namespace ConsoleApp1
{
    /// <summary>ตรวจรูปแบบป้ายไทยหลัง normalize — ใช้กรอง log/แสดงผล</summary>
    internal static class ThaiPlateResultValidator
    {
        public static bool IsValid(string plateNumber, out string reason)
        {
            reason = string.Empty;

            if (string.IsNullOrWhiteSpace(plateNumber))
            {
                reason = "empty";
                return false;
            }

            ThaiPlateParts.Split(plateNumber.Trim(), out string lettersPart, out string digitsPart);

            if (!ThaiPlateCharset.IsValidPlateDigits(digitsPart))
            {
                reason = "digits";
                return false;
            }

            int consonantStart = 0;
            while (consonantStart < lettersPart.Length && char.IsDigit(lettersPart[consonantStart]))
                consonantStart++;

            string consonants = lettersPart[consonantStart..];
            if (consonants.Any(c => !ThaiPlateCharset.IsPlateConsonant(c)))
            {
                reason = "prefix";
                return false;
            }
            int consonantCount = CountThaiConsonants(consonants);

            if (consonantCount >= 1 && consonantCount <= 2)
                return true;

            reason = "prefix";
            return false;
        }

        private static int CountThaiConsonants(string text)
        {
            int count = 0;
            foreach (char c in text)
            {
                if (ThaiPlateCharset.IsPlateConsonant(c))
                    count++;
            }

            return count;
        }
    }
}
