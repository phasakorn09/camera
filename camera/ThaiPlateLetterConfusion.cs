using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ConsoleApp1
{
    /// <summary>พยัญชนะไทยที่ OCR มักสับสน — ใช้ vote / แก้หลัง OCR</summary>
    internal static class ThaiPlateLetterConfusion
    {
        private static readonly char[][] SimilarGroups =
        {
            new[] { 'ก', 'ค', 'ฅ', 'ฆ' },
            new[] { 'ข', 'ช', 'ฉ', 'ซ' },
            new[] { 'ศ', 'ษ', 'ส', 'ข', 'ช' },
            new[] { 'ผ', 'ฝ', 'พ', 'ฟ', 'ภ' },
            new[] { 'ท', 'ธ', 'ฑ', 'ฒ', 'ต' },
            new[] { 'ร', 'ฐ', 'ฏ', 'ฎ', 'ล', 'ฤ' },
            new[] { 'น', 'ญ', 'ณ', 'ย', 'ร' },
            new[] { 'ด', 'ต', 'ถ' },
            new[] { 'บ', 'ป' },
            new[] { 'ม', 'น' },
            new[] { 'ว', 'ง' },
            new[] { 'ห', 'อ', 'ฮ' },
            new[] { 'ฬ', 'ล' },
        };

        private static readonly Dictionary<string, string> SafePrefixFixes = new(StringComparer.Ordinal)
        {
            ["สข"] = "สช",
            ["รฐ"] = "ฐฐ",
            ["รร"] = "ฐฐ",
            ["ธร"] = "ฐฐ",
            ["5ร"] = "ฐฐ",
            ["25ร"] = "ฐฐ",
        };

        private static readonly Dictionary<char, char[]> PartnerCache = BuildPartnerCache();

        public static IReadOnlyList<char> GetPartners(char c)
        {
            return PartnerCache.TryGetValue(c, out char[]? partners)
                ? partners
                : Array.Empty<char>();
        }

        public static bool IsLikelyMisread(char read, char expected)
        {
            if (read == expected)
                return false;

            return GetPartners(expected).Contains(read);
        }

        /// <summary>แก้ prefix หลัง OCR — กฎที่ปลอดภัย + vote ตำแหน่ง</summary>
        public static string FixPrefix(string letters)
        {
            if (string.IsNullOrEmpty(letters))
                return letters;

            if (SafePrefixFixes.TryGetValue(letters, out string? fixedPrefix))
                return fixedPrefix;

            var sb = new StringBuilder(letters);
            ApplyStringBuilderFixes(sb);
            return sb.ToString();
        }

        public static void ApplyStringBuilderFixes(StringBuilder letters)
        {
            int consonantStart = 0;
            while (consonantStart < letters.Length && char.IsDigit(letters[consonantStart]))
                consonantStart++;

            string consonants = letters.ToString(consonantStart, letters.Length - consonantStart);
            if (SafePrefixFixes.TryGetValue(consonants, out string? fixedPrefix))
            {
                letters.Remove(consonantStart, letters.Length - consonantStart);
                letters.Append(fixedPrefix);
            }
        }

        /// <summary>โหวต prefix ทีละตำแหน่ง + soft vote ตัวที่คล้ายกัน</summary>
        public static string VotePrefix(IEnumerable<(string Letters, float Weight)> samples)
        {
            var list = samples
                .Where(s => !string.IsNullOrWhiteSpace(s.Letters))
                .ToList();

            if (list.Count == 0)
                return string.Empty;

            int maxLen = Math.Min(3, list.Max(s => s.Letters.Length));
            var sb = new StringBuilder(maxLen);

            for (int pos = 0; pos < maxLen; pos++)
            {
                var votes = new Dictionary<char, float>();
                foreach (var (letters, weight) in list)
                {
                    if (pos >= letters.Length)
                        continue;

                    char c = letters[pos];
                    if (!IsPlateLetterChar(c))
                        continue;

                    votes[c] = votes.GetValueOrDefault(c) + weight;
                    foreach (char alt in GetPartners(c))
                        votes[alt] = votes.GetValueOrDefault(alt) + weight * 0.42f;
                }

                if (votes.Count == 0)
                    continue;

                sb.Append(votes.OrderByDescending(kv => kv.Value).First().Key);
            }

            return FixPrefix(sb.ToString());
        }

        /// <summary>รวม sample ที่ prefix ใกล้เคียง (สำหรับ vote ตัวเลขต่อ)</summary>
        public static bool PrefixMatches(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
                return a == b;

            a = FixPrefix(a.Trim());
            b = FixPrefix(b.Trim());

            if (a == b)
                return true;

            if (a.Length != b.Length)
                return false;

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] == b[i])
                    continue;

                if (!IsLikelyMisread(a[i], b[i]) && !IsLikelyMisread(b[i], a[i]))
                    return false;
            }

            return true;
        }

        public static string NormalizeForVote(string letters) => FixPrefix(letters.Trim());

        private static Dictionary<char, char[]> BuildPartnerCache()
        {
            var map = new Dictionary<char, List<char>>();

            void AddPair(char a, char b)
            {
                if (!map.TryGetValue(a, out List<char>? listA))
                {
                    listA = new List<char>();
                    map[a] = listA;
                }

                if (!listA.Contains(b))
                    listA.Add(b);
            }

            foreach (char[] group in SimilarGroups)
            {
                foreach (char a in group)
                {
                    foreach (char b in group)
                    {
                        if (a != b)
                            AddPair(a, b);
                    }
                }
            }

            return map.ToDictionary(
                kv => kv.Key,
                kv => kv.Value.ToArray(),
                comparer: EqualityComparer<char>.Default);
        }

        private static bool IsPlateLetterChar(char c) =>
            char.IsDigit(c) || (c >= '\u0E01' && c <= '\u0E2E');
    }
}
