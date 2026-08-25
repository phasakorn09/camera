using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ConsoleApp1
{
    /// <summary>พยัญชนะไทยที่ OCR มักสับสน — แก้ prefix หลัง OCR</summary>
    internal static class ThaiPlateLetterConfusion
    {
        private static readonly char[][] SimilarGroups =
        {
            // ใช้ใน OCR logit ambiguity เท่านั้น — ไม่ soft vote ใน tracker
            new[] { 'ก', 'ค', 'ฅ', 'ฆ' },
            new[] { 'จ', 'ช', 'ซ', 'ฌ' },
            new[] { 'ด', 'ต', 'ถ', 'ท', 'ฒ', 'ฏ' },
            new[] { 'ฎ', 'ด', 'ธ' },
            new[] { 'บ', 'ป', 'พ', 'ฟ' },
            new[] { 'ส', 'ศ', 'ษ' },
            new[] { 'น', 'ณ' },
            new[] { 'ญ', 'ย', 'น' },
            new[] { 'ฬ', 'ล', 'ห' },
            new[] { 'ฮ', 'ห', 'อ' },
            new[] { 'ข', 'ฃ', 'ค' },
            new[] { 'ม', 'น' },
        };

        /// <summary>พยัญชนะบนป้ายที่ OCR มักทิ้งหรือสลับเป็นตัวที่พบบ่อยกว่า</summary>
        private static readonly HashSet<char> RarePlateConsonants = new()
        {
            'ฆ', 'ญ', 'ณ', 'ฬ', 'ฮ'
        };

        private static readonly Dictionary<string, string> SafePrefixFixes = new(StringComparer.Ordinal)
        {
            ["สข"] = "สช",
            ["รฐ"] = "ฐฐ",
            ["ฐร"] = "ฐฐ",
            ["ฐย"] = "ฐฐ",
            ["ลฐ"] = "ฐฐ",
            ["ยฐ"] = "ฐฐ",
            ["นฐ"] = "ฐฐ",
            ["รฏ"] = "ฐฐ",
        };

        private static readonly HashSet<char> ThoPlateNoiseChars = new()
        {
            'ฐ', 'ร', 'ล', 'ฏ', 'ย', 'น', 'ธ'
        };

        private static readonly Dictionary<char, char[]> PartnerCache = BuildPartnerCache();

        public static bool IsRarePlateConsonant(char c) =>
            RarePlateConsonants.Contains(c);

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

        /// <summary>แก้ prefix หลัง OCR — กฎที่ปลอดภัยเท่านั้น</summary>
        public static string FixPrefix(string letters, string digitHint = "")
        {
            if (string.IsNullOrEmpty(letters))
                return letters;

            int consonantStart = 0;
            while (consonantStart < letters.Length && char.IsDigit(letters[consonantStart]))
                consonantStart++;

            string leading = letters[..consonantStart];
            string consonants = letters[consonantStart..];

            if (SafePrefixFixes.TryGetValue(consonants, out string? fixedPrefix)
                && ShouldApplySafePrefixFix(consonants, fixedPrefix, digitHint))
                return leading + fixedPrefix;

            string thoFixed = TryFixThoThoPrefix(consonants);
            if (thoFixed != consonants)
                return leading + thoFixed;

            return letters;
        }

        private static bool ShouldApplySafePrefixFix(string consonants, string fixedPrefix, string digitHint)
        {
            if (consonants == "รฏ" && fixedPrefix == "ฐฐ")
                return digitHint.StartsWith("69", StringComparison.Ordinal);

            return true;
        }

        /// <summary>ฐฐ 69 — OCR ได้ ฐร / ลฐ / รฏ / ฐรฐ</summary>
        public static bool IsThoThoLikePrefix(string letters)
        {
            if (string.IsNullOrEmpty(letters))
                return false;

            int start = 0;
            while (start < letters.Length && char.IsDigit(letters[start]))
                start++;

            return TryFixThoThoPrefix(letters[start..]) == "ฐฐ";
        }

        private static string TryFixThoThoPrefix(string consonants)
        {
            if (consonants.Length < 2 || consonants.Length > 3)
                return consonants;

            if (!consonants.Contains('ฐ'))
                return consonants;

            if (!consonants.All(c => ThoPlateNoiseChars.Contains(c)))
                return consonants;

            return "ฐฐ";
        }

        public static void ApplyStringBuilderFixes(StringBuilder letters)
        {
            string fixedPrefix = FixPrefix(letters.ToString());
            letters.Clear();
            letters.Append(fixedPrefix);
        }

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
    }
}
