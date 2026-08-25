using System;
using System.Collections.Generic;
using System.Linq;

namespace ConsoleApp1
{
    /// <summary>ให้คะแนน prefix หลัง normalize — ช่วยเลือก ฒก แทน ตก เมื่อ logit ใกล้กัน</summary>
    internal static class PlatePrefixScorer
    {
        public static string ExtractPrefixConsonants(string normalizedPlate)
        {
            if (string.IsNullOrWhiteSpace(normalizedPlate))
                return string.Empty;

            ThaiPlateParts.Split(normalizedPlate.Trim(), out string lettersPart, out _);

            int start = 0;
            while (start < lettersPart.Length && char.IsDigit(lettersPart[start]))
                start++;

            var consonants = new List<char>(2);
            for (int i = start; i < lettersPart.Length && consonants.Count < 2; i++)
            {
                char c = lettersPart[i];
                if (ThaiPlateCharset.IsPlateConsonant(c))
                    consonants.Add(c);
            }

            return new string(consonants.ToArray());
        }

        public static string ExtractDigitPart(string normalizedPlate)
        {
            if (string.IsNullOrWhiteSpace(normalizedPlate))
                return string.Empty;

            ThaiPlateParts.Split(normalizedPlate.Trim(), out _, out string digitsPart);
            return digitsPart;
        }

        public static float ScorePlateNumber(
            string normalized,
            IReadOnlyList<(int Time, int ClassIdx, float Score)>? peaks = null)
        {
            if (string.IsNullOrWhiteSpace(normalized))
                return float.MinValue;

            int letterCount = CountPrefixConsonants(normalized);
            float score = letterCount switch
            {
                2 => 0.75f,
                1 => -0.15f,
                >= 3 => -0.85f,
                _ => 0f
            };

            string digits = ExtractDigitPart(normalized);
            if (digits.Length >= 1 && digits.Length <= 4 && digits.All(char.IsDigit))
                score += 0.2f;

            if (peaks != null && peaks.Count > 0)
                score += peaks.Average(p => p.Score) * 0.08f;

            return score;
        }

        private static int CountPrefixConsonants(string normalized)
        {
            int space = normalized.IndexOf(' ');
            ReadOnlySpan<char> letterPart = space > 0
                ? normalized.AsSpan(0, space)
                : normalized.AsSpan();

            int count = 0;
            foreach (char c in letterPart)
            {
                if (ThaiPlateCharset.IsPlateConsonant(c))
                    count++;
            }

            return count;
        }
    }
}
