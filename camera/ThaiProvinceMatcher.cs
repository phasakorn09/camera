using System;
using System.Collections.Generic;
using System.Text;

namespace ConsoleApp1
{
    /// <summary>จับคู่ OCR จังหวัด → ชื่อมาตรฐาน 77 จังหวัด (ไม่คืนข้อความดิบถ้าจับไม่ได้)</summary>
    internal static class ThaiProvinceMatcher
    {
        private static readonly string[] Provinces = ThaiPlateCharset.Provinces;

        public static string Match(string ocrText)
        {
            if (string.IsNullOrWhiteSpace(ocrText))
                return string.Empty;

            string normalized = Normalize(ocrText);
            if (normalized.Length == 0)
                return string.Empty;

            string skeleton = ConsonantSkeleton(normalized);
            if (skeleton.Length == 0)
                return string.Empty;

            foreach (var province in Provinces)
            {
                if (normalized == province)
                    return province;
            }

            string? exactSkeleton = null;
            int exactCount = 0;
            foreach (var province in Provinces)
            {
                if (ConsonantSkeleton(province) != skeleton)
                    continue;
                exactSkeleton = province;
                exactCount++;
            }
            if (exactCount == 1)
                return exactSkeleton!;

            if (skeleton.Length < 3)
                return string.Empty;

            string? best = null;
            int bestScore = int.MaxValue;

            foreach (var province in Provinces)
            {
                string pSkeleton = ConsonantSkeleton(province);
                if (pSkeleton.Length < 3)
                    continue;

                if (skeleton.Contains(pSkeleton, StringComparison.Ordinal)
                    || pSkeleton.Contains(skeleton, StringComparison.Ordinal))
                {
                    int containScore = Math.Abs(skeleton.Length - pSkeleton.Length);
                    if (containScore < bestScore)
                    {
                        bestScore = containScore;
                        best = province;
                    }
                    continue;
                }

                if (skeleton.Length < 4)
                    continue;

                int distSkel = Levenshtein(skeleton, pSkeleton);
                int distFull = Levenshtein(normalized, province);
                int score = Math.Min(distSkel, distFull);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = province;
                }
            }

            if (best == null)
                return string.Empty;

            int threshold = Math.Max(1, ConsonantSkeleton(best).Length / 4);
            return bestScore <= threshold ? best : string.Empty;
        }

        private static string Normalize(string text)
        {
            var chars = new List<char>(text.Length);
            foreach (char c in text)
            {
                if (ThaiPlateCharset.IsProvinceChar(c))
                    chars.Add(c);
            }
            return new string(chars.ToArray());
        }

        private static string ConsonantSkeleton(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (ThaiPlateCharset.IsPlateConsonant(c))
                    sb.Append(c);
            }
            return sb.ToString();
        }

        private static int Levenshtein(string a, string b)
        {
            if (a.Length == 0) return b.Length;
            if (b.Length == 0) return a.Length;

            var dp = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) dp[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) dp[0, j] = j;

            for (int i = 1; i <= a.Length; i++)
            {
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    dp[i, j] = Math.Min(
                        Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                        dp[i - 1, j - 1] + cost);
                }
            }

            return dp[a.Length, b.Length];
        }
    }
}
