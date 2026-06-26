using System;
using System.Collections.Generic;
using System.Text;

namespace ConsoleApp1
{
    /// <summary>จับคู่ OCR จังหวัด → ชื่อมาตรฐาน 77 จังหวัด (ไม่คืนข้อความดิบถ้าจับไม่ได้)</summary>
    internal static class ThaiProvinceMatcher
    {
        private static readonly string[] Provinces =
        {
            "\u0E01\u0E23\u0E38\u0E07\u0E40\u0E17\u0E1E\u0E21\u0E2B\u0E32\u0E19\u0E04\u0E23",
            "\u0E01\u0E23\u0E30\u0E1A\u0E35",
            "\u0E01\u0E32\u0E0D\u0E08\u0E19\u0E1A\u0E38\u0E23\u0E35",
            "\u0E01\u0E32\u0E0E\u0E2A\u0E34\u0E19\u0E18\u0E38\u0E4C",
            "\u0E01\u0E33\u0E41\u0E1E\u0E07\u0E40\u0E1E\u0E0A\u0E23",
            "\u0E02\u0E2D\u0E19\u0E41\u0E01\u0E48\u0E19",
            "\u0E08\u0E31\u0E19\u0E17\u0E1A\u0E38\u0E23\u0E35",
            "\u0E09\u0E30\u0E40\u0E0A\u0E34\u0E07\u0E40\u0E17\u0E23\u0E32",
            "\u0E0A\u0E25\u0E1A\u0E38\u0E23\u0E35",
            "\u0E0A\u0E31\u0E22\u0E19\u0E32\u0E17",
            "\u0E0A\u0E31\u0E22\u0E20\u0E39\u0E21\u0E34",
            "\u0E0A\u0E38\u0E21\u0E1E\u0E23",
            "\u0E40\u0E0A\u0E35\u0E22\u0E07\u0E23\u0E32\u0E22",
            "\u0E40\u0E0A\u0E35\u0E22\u0E07\u0E43\u0E2B\u0E21\u0E48",
            "\u0E15\u0E23\u0E31\u0E07",
            "\u0E15\u0E23\u0E32\u0E14",
            "\u0E15\u0E32\u0E01",
            "\u0E19\u0E04\u0E23\u0E19\u0E32\u0E22\u0E01",
            "\u0E19\u0E04\u0E23\u0E1B\u0E10\u0E21",
            "\u0E19\u0E04\u0E23\u0E1E\u0E19\u0E21",
            "\u0E19\u0E04\u0E23\u0E23\u0E32\u0E0A\u0E2A\u0E35\u0E21\u0E32",
            "\u0E19\u0E04\u0E23\u0E28\u0E23\u0E35\u0E18\u0E23\u0E23\u0E21\u0E23\u0E32\u0E0A",
            "\u0E19\u0E04\u0E23\u0E2A\u0E27\u0E23\u0E23\u0E04\u0E4C",
            "\u0E19\u0E19\u0E17\u0E1A\u0E38\u0E23\u0E35",
            "\u0E19\u0E23\u0E32\u0E18\u0E34\u0E27\u0E32\u0E2A",
            "\u0E19\u0E48\u0E32\u0E19",
            "\u0E1A\u0E36\u0E07\u0E01\u0E32\u0E0E",
            "\u0E1A\u0E38\u0E23\u0E35\u0E23\u0E31\u0E21\u0E22\u0E4C",
            "\u0E1B\u0E17\u0E38\u0E21\u0E18\u0E32\u0E19\u0E35",
            "\u0E1B\u0E23\u0E30\u0E08\u0E27\u0E1A\u0E04\u0E35\u0E23\u0E35\u0E02\u0E31\u0E19\u0E18\u0E4C",
            "\u0E1B\u0E23\u0E32\u0E08\u0E35\u0E19\u0E1A\u0E38\u0E23\u0E35",
            "\u0E1B\u0E31\u0E15\u0E15\u0E32\u0E19\u0E35",
            "\u0E1E\u0E23\u0E30\u0E19\u0E04\u0E23\u0E28\u0E23\u0E35\u0E2D\u0E22\u0E38\u0E18\u0E22\u0E32",
            "\u0E1E\u0E31\u0E07\u0E07\u0E32",
            "\u0E1E\u0E31\u0E17\u0E25\u0E38\u0E07",
            "\u0E1E\u0E34\u0E08\u0E34\u0E15\u0E23",
            "\u0E1E\u0E34\u0E29\u0E13\u0E38\u0E42\u0E25\u0E01",
            "\u0E40\u0E1E\u0E0A\u0E23\u0E1A\u0E38\u0E23\u0E35",
            "\u0E40\u0E1E\u0E0A\u0E23\u0E1A\u0E39\u0E23\u0E13\u0E4C",
            "\u0E41\u0E1E\u0E23\u0E48",
            "\u0E20\u0E39\u0E40\u0E01\u0E47\u0E15",
            "\u0E21\u0E2B\u0E32\u0E2A\u0E32\u0E23\u0E04\u0E32\u0E21",
            "\u0E21\u0E38\u0E01\u0E14\u0E32\u0E2B\u0E32\u0E23",
            "\u0E41\u0E21\u0E48\u0E2E\u0E48\u0E2D\u0E07\u0E2A\u0E2D\u0E19",
            "\u0E22\u0E42\u0E02\u0E18\u0E23",
            "\u0E22\u0E30\u0E25\u0E32",
            "\u0E23\u0E49\u0E2D\u0E22\u0E40\u0E2D\u0E47\u0E14",
            "\u0E23\u0E30\u0E19\u0EAD\u0E07",
            "\u0E23\u0E30\u0E22\u0E2D\u0E07",
            "\u0E23\u0E32\u0E0A\u0E1A\u0E38\u0E23\u0E35",
            "\u0E25\u0E1E\u0E1A\u0E38\u0E23\u0E35",
            "\u0E25\u0E33\u0E1B\u0E32\u0E07",
            "\u0E25\u0E33\u0E1E\u0E39\u0E19",
            "\u0E40\u0E25\u0E22",
            "\u0E28\u0E23\u0E35\u0E2A\u0E30\u0E40\u0E01\u0E29",
            "\u0E2A\u0E01\u0E25\u0E19\u0E04\u0E23",
            "\u0E2A\u0E07\u0E02\u0E25\u0E32",
            "\u0E2A\u0E15\u0E39\u0E25",
            "\u0E2A\u0E21\u0E38\u0E17\u0E23\u0E1B\u0E23\u0E32\u0E01\u0E32\u0E23",
            "\u0E2A\u0E21\u0E38\u0E17\u0E23\u0E2A\u0E07\u0E04\u0E23\u0E32\u0E21",
            "\u0E2A\u0E21\u0E38\u0E17\u0E23\u0E2A\u0E32\u0E04\u0E23",
            "\u0E2A\u0E23\u0E30\u0E41\u0E01\u0E49\u0E27",
            "\u0E2A\u0E23\u0E30\u0E1A\u0E38\u0E23\u0E35",
            "\u0E2A\u0E34\u0E07\u0E2B\u0E4C\u0E1A\u0E38\u0E23\u0E35",
            "\u0E2A\u0E38\u0E42\u0E02\u0E17\u0E31\u0E22",
            "\u0E2A\u0E38\u0E1E\u0E23\u0E23\u0E13\u0E1A\u0E38\u0E23\u0E35",
            "\u0E2A\u0E38\u0E23\u0E32\u0E29\u0E0E\u0E23\u0E18\u0E32\u0E19\u0E35",
            "\u0E2A\u0E38\u0E23\u0E34\u0E19\u0E17\u0E23\u0E4C",
            "\u0E2B\u0E19\u0E2D\u0E07\u0E04\u0E32\u0E22",
            "\u0E2B\u0E19\u0E2D\u0E07\u0E1A\u0E31\u0E27\u0E25\u0E33\u0E20\u0E39",
            "\u0E2D\u0E48\u0E32\u0E07\u0E17\u0E2D\u0E07",
            "\u0E2D\u0E33\u0E19\u0E32\u0E08\u0E40\u0E08\u0E23\u0E34\u0E0D",
            "\u0E2D\u0E38\u0E14\u0E23\u0E18\u0E32\u0E19\u0E35",
            "\u0E2D\u0E38\u0E15\u0E23\u0E14\u0E34\u0E15\u0E16\u0E4C",
            "\u0E2D\u0E38\u0E17\u0E31\u0E22\u0E18\u0E32\u0E19\u0E35",
            "\u0E2D\u0E38\u0E1A\u0E25\u0E23\u0E32\u0E0A\u0E18\u0E32\u0E19\u0E35"
        };

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

            string? best = null;
            int bestScore = int.MaxValue;

            foreach (var province in Provinces)
            {
                string pSkeleton = ConsonantSkeleton(province);

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

            int threshold = Math.Max(2, ConsonantSkeleton(best).Length / 3);
            return bestScore <= threshold ? best : string.Empty;
        }

        private static string Normalize(string text)
        {
            var chars = new List<char>(text.Length);
            foreach (char c in text)
            {
                if (IsThaiScriptChar(c))
                    chars.Add(c);
            }
            return new string(chars.ToArray());
        }

        private static string ConsonantSkeleton(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (c >= '\u0E01' && c <= '\u0E2E')
                    sb.Append(c);
            }
            return sb.ToString();
        }

        private static bool IsThaiScriptChar(char c)
        {
            if (c >= '\u0E01' && c <= '\u0E2E') return true;
            if (c >= '\u0E30' && c <= '\u0E3A') return true;
            if (c >= '\u0E40' && c <= '\u0E4E') return true;
            return false;
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
