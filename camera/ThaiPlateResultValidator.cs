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

            SplitPlateNumberParts(plateNumber.Trim(), out string lettersPart, out string digitsPart);

            if (string.IsNullOrEmpty(digitsPart) || digitsPart.Length > 4 || !digitsPart.All(char.IsDigit))
            {
                reason = "digits";
                return false;
            }

            int consonantStart = 0;
            while (consonantStart < lettersPart.Length && char.IsDigit(lettersPart[consonantStart]))
                consonantStart++;

            string consonants = lettersPart[consonantStart..];
            int consonantCount = CountThaiConsonants(consonants);

            if (consonantCount >= 1 && consonantCount <= 2)
                return true;

            if (consonants == "ฐฐ" || ThaiPlateLetterConfusion.IsThoThoLikePrefix(consonants))
                return true;

            if (consonantCount >= 2 && consonants.Length >= 2 && consonants[0] == consonants[1])
                return true;

            reason = "prefix";
            return false;
        }

        private static void SplitPlateNumberParts(string plateNumber, out string lettersPart, out string digitsPart)
        {
            lettersPart = string.Empty;
            digitsPart = string.Empty;

            int space = plateNumber.IndexOf(' ');
            if (space > 0)
            {
                lettersPart = plateNumber[..space].Trim();
                digitsPart = plateNumber[(space + 1)..].Trim();
                return;
            }

            int firstDigit = -1;
            for (int i = 0; i < plateNumber.Length; i++)
            {
                if (char.IsDigit(plateNumber[i]))
                {
                    firstDigit = i;
                    break;
                }
            }

            if (firstDigit < 0)
            {
                lettersPart = plateNumber;
                return;
            }

            lettersPart = plateNumber[..firstDigit].Trim();
            digitsPart = plateNumber[firstDigit..].Trim();
        }

        private static int CountThaiConsonants(string text)
        {
            int count = 0;
            foreach (char c in text)
            {
                if (c >= '\u0E01' && c <= '\u0E2E')
                    count++;
            }

            return count;
        }
    }
}
