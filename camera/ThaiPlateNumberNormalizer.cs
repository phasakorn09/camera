using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ConsoleApp1
{
    /// <summary>
    /// ทำความสะอาดเลขทะเบียนบรรทัดบน (พยัญชนะ + ตัวเลข)
    /// หลักการ: เก็บลำดับเลขจาก OCR, ตัด noise ชัดเจนเท่านั้น — ไม่เดาเลขใหม่ (9299 ≠ 9999)
    /// </summary>
    internal static class ThaiPlateNumberNormalizer
    {
        private const int MaxDigits = 4;
        private const int MaxConsonants = 3;
        private const int HomogeneousSpamRun = 8;

        private static readonly HashSet<char> NoiseLeadConsonants = new() { 'อ', 'ล', 'ร' };

        public static string Normalize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            var stream = new List<char>();
            foreach (char c in raw)
            {
                if (char.IsDigit(c))
                    stream.Add(c);
                else if (IsThaiConsonant(c))
                    stream.Add(c);
                else if (IsThaiDigit(c))
                    stream.Add((char)('0' + (c - '\u0E50')));
            }

            if (stream.Count == 0)
                return string.Empty;

            int firstConsonantIdx = stream.FindIndex(IsThaiConsonant);
            if (firstConsonantIdx < 0)
            {
                string digitsOnly = StripLeadingDigitNoise(new string(stream.Where(char.IsDigit).ToArray()));
                return FinalizeDigits(ExtractBestDigits(digitsOnly), string.Empty);
            }

            var letters = new StringBuilder();

            if (firstConsonantIdx > 0)
            {
                var leadingDigits = stream.Take(firstConsonantIdx).ToArray();
                if (IsValidLeadingDigits(leadingDigits))
                {
                    foreach (char d in leadingDigits)
                        letters.Append(d);
                }
            }

            int i = firstConsonantIdx;
            int consonantCount = 0;
            while (i < stream.Count && IsThaiConsonant(stream[i]) && consonantCount < MaxConsonants + 2)
            {
                letters.Append(stream[i]);
                i++;
                consonantCount++;
            }

            var digitChars = new List<char>();
            for (; i < stream.Count; i++)
            {
                if (char.IsDigit(stream[i]))
                    digitChars.Add(stream[i]);
            }

            FixLetterMisreads(letters);

            string lettersFinal = letters.ToString();
            string digitsRaw = ExtractBestDigits(new string(digitChars.ToArray()));
            string digitsFinal = FinalizeDigits(digitsRaw, lettersFinal);

            if (lettersFinal.Length == 0 && digitsFinal.Length == 0)
                return string.Empty;
            if (digitsFinal.Length == 0)
                return lettersFinal;
            if (lettersFinal.Length == 0)
                return digitsFinal;

            return $"{lettersFinal} {digitsFinal}";
        }

        private static bool IsValidLeadingDigits(char[] leadingDigits)
        {
            if (leadingDigits.Length == 0)
                return false;

            if (leadingDigits.Length == 1 && (leadingDigits[0] == '1' || leadingDigits[0] == '2'))
                return false;

            return leadingDigits.All(char.IsDigit);
        }

        private static void FixLetterMisreads(StringBuilder letters)
        {
            int consonantStart = 0;
            while (consonantStart < letters.Length && char.IsDigit(letters[consonantStart]))
                consonantStart++;

            for (int j = consonantStart; j < letters.Length; j++)
            {
                if (letters[j] == '0')
                    letters[j] = 'ก';
            }

            FixRedPlateLetters(letters, consonantStart);
            TrimLeadingNoiseConsonants(letters, consonantStart);

            string consonants = letters.ToString(consonantStart, letters.Length - consonantStart);

            if (consonants == "รฐ")
                letters[consonantStart] = 'ฐ';

            if (consonants == "ว")
                letters.Insert(consonantStart, 'ก');
            else if (consonants == "ย")
                letters.Insert(consonantStart, 'ก');

            if (letters.Length > consonantStart + 1
                && letters[^1] == '1'
                && IsThaiConsonant(letters[^2]))
            {
                letters[^1] = 'ท';
            }
        }

        private static void FixRedPlateLetters(StringBuilder letters, int consonantStart)
        {
            if (letters.Length <= consonantStart || letters[consonantStart] != '6')
                return;

            string tail = letters.ToString(consonantStart + 1, letters.Length - consonantStart - 1);
            if (tail == "ก")
                letters.Append('ท');
            else if (tail == "ท")
                letters.Insert(consonantStart + 1, 'ก');
            else if (tail == "ก1")
                letters[^1] = 'ท';
        }

        private static void TrimLeadingNoiseConsonants(StringBuilder letters, int consonantStart)
        {
            while (letters.Length - consonantStart > 2
                   && NoiseLeadConsonants.Contains(letters[consonantStart]))
            {
                letters.Remove(consonantStart, 1);
            }
        }

        private static string FinalizeDigits(string digits, string letters)
        {
            if (string.IsNullOrEmpty(digits))
                return string.Empty;

            // 6660 / 2220 / 9990 — ท้าย 0 noise (ต้องทำก่อน early return len==4)
            digits = FixSameDigitTrailingZero(digits);

            // ฐฐ 69 — OCR ต่อ 6903, 6933, 6934
            digits = TrimShortPlateExtension(digits, letters);

            // 666 → 6666 (เลขเดียวกัน 3 หลัก — ไม่แตะ 929)
            if (digits.Length == 3 && digits.All(c => c == digits[0]))
                return new string(digits[0], MaxDigits);

            // ครบ 4 หลัก mixed — เก็บตาม OCR (9299, 1441, 6688)
            if (digits.Length == MaxDigits)
                return digits;

            // 688 / 866 → 6688
            if (TryCompleteDoublePair(digits, out string pair))
                return pair;

            return digits;
        }

        /// <summary>6660, 2220, 9990 → 6666, 2222, 9999</summary>
        private static string FixSameDigitTrailingZero(string digits)
        {
            if (digits.Length >= 3
                && digits[^1] == '0'
                && digits[..^1].All(c => c == digits[0]))
            {
                return new string(digits[0], MaxDigits);
            }

            return digits;
        }

        /// <summary>ป้ายเลขสั้น (ฐฐ 69) — ตัดเลข garbage ท้าย 6903 → 69</summary>
        private static string TrimShortPlateExtension(string digits, string letters)
        {
            if (digits.Length != MaxDigits || digits.Length < 3)
                return digits;

            if (!digits.StartsWith("69", StringComparison.Ordinal))
                return digits;

            // ฐฐ 69 ชัดเจน
            if (letters == "ฐฐ")
                return digits[..2];

            if (IsSixNineTrailingNoise(digits))
                return digits[..2];

            return digits;
        }

        /// <summary>6903, 6933, 6934 — ไม่ตัด 6912, 6923</summary>
        private static bool IsSixNineTrailingNoise(string digits)
        {
            if (digits.Length != 4)
                return false;

            char c2 = digits[2];
            char c3 = digits[3];

            if (c2 == '0')
                return true;

            if (c2 == '3' && c3 is '3' or '4')
                return true;

            if (c2 == c3 && c2 is not '6' and not '9')
                return true;

            return false;
        }

        /// <summary>0266 / 2666 → 6666 (noise นำหน้าเลข vanity)</summary>
        private static string StripLeadingDigitNoise(string digits)
        {
            while (digits.Length > 0 && digits[0] == '0')
                digits = digits[1..];

            if (digits.Length >= 4 && digits[0] is '1' or '2')
            {
                string rest = digits[1..];
                if (rest.Length >= 3 && rest.Take(3).All(c => c == rest[0]))
                    digits = rest;
            }

            return digits;
        }

        /// <summary>688, 866, 6880(→688) — ไม่ใช้กับ 9299, 929, 1441</summary>
        private static bool TryCompleteDoublePair(string digits, out string result)
        {
            result = string.Empty;
            string d = digits;

            if (d.Length == 4 && d.EndsWith('0') && d.Count(c => c == '0') == 1)
                d = d[..^1];

            if (d.Length != 3)
                return false;

            // XYY → XXYY
            if (d[1] == d[2] && d[0] != d[1])
            {
                result = $"{d[0]}{d[0]}{d[1]}{d[1]}";
                return true;
            }

            // XXY → XXYY
            if (d[0] == d[1] && d[1] != d[2])
            {
                result = $"{d[0]}{d[0]}{d[2]}{d[2]}";
                return true;
            }

            return false;
        }

        private static string ExtractBestDigits(string digits)
        {
            if (string.IsNullOrEmpty(digits))
                return string.Empty;

            digits = TrimTrailingDigitNoise(digits);
            if (digits.Length == 0)
                return string.Empty;

            if (TryExtractHomogeneousSpam(digits, out string homogeneous))
                return homogeneous;

            digits = CollapseOcrDigitRuns(digits);

            if (digits.Length <= MaxDigits)
                return digits;

            return PickBestFourDigitWindow(digits);
        }

        /// <summary>ย่อ run ซ้ำจาก peak OCR — เก็บลำดับ ไม่ re-distribute</summary>
        private static string CollapseOcrDigitRuns(string digits)
        {
            var runs = MergeAdjacentRuns(GetRuns(digits));
            var sb = new StringBuilder(digits.Length);

            foreach (var (ch, len) in runs)
            {
                int uniqueCount = digits.Where(char.IsDigit).Select(c => c).Distinct().Count();
                int cap = len >= HomogeneousSpamRun
                    ? MaxDigits
                    : uniqueCount <= 2 && len >= 4
                        ? 2
                        : len;

                sb.Append(new string(ch, Math.Min(len, cap)));
            }

            return sb.ToString();
        }

        /// <summary>เลือก 4 หลักที่ diversity สูงสุด — 9299 ชนะ 9999 ใน 99992999</summary>
        private static string PickBestFourDigitWindow(string digits)
        {
            if (digits.Length <= MaxDigits)
                return digits;

            string best = digits[..MaxDigits];
            int bestScore = ScoreDigitWindow(best);

            for (int i = 1; i <= digits.Length - MaxDigits; i++)
            {
                string window = digits.Substring(i, MaxDigits);
                int score = ScoreDigitWindow(window);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = window;
                }
            }

            return best;
        }

        private static int ScoreDigitWindow(string window)
        {
            if (window.Length != MaxDigits)
                return 0;

            int unique = window.Distinct().Count();
            int score = unique * 100;

            if (IsAabbPattern(window))
                score += 30;

            if (window.All(c => c == window[0]))
                score += 40;

            return score;
        }

        private static bool TryExtractHomogeneousSpam(string digits, out string result)
        {
            result = string.Empty;
            if (digits.Length < HomogeneousSpamRun)
                return false;

            int longestRun = GetLongestSameDigitRun(digits);
            if (longestRun < HomogeneousSpamRun)
                return false;

            if (digits.All(c => c == digits[0]))
            {
                result = new string(digits[0], MaxDigits);
                return true;
            }

            return false;
        }

        private static bool IsAabbPattern(string s) =>
            s.Length == MaxDigits && s[0] == s[1] && s[2] == s[3] && s[0] != s[2];

        private static List<(char Ch, int Len)> MergeAdjacentRuns(List<(char Ch, int Len)> runs)
        {
            if (runs.Count == 0)
                return runs;

            var merged = new List<(char Ch, int Len)> { runs[0] };
            for (int i = 1; i < runs.Count; i++)
            {
                var last = merged[^1];
                if (runs[i].Ch == last.Ch)
                    merged[^1] = (last.Ch, last.Len + runs[i].Len);
                else
                    merged.Add(runs[i]);
            }

            return merged;
        }

        private static List<(char Ch, int Len)> GetRuns(string digits)
        {
            var runs = new List<(char Ch, int Len)>();
            char ch = digits[0];
            int len = 1;

            for (int i = 1; i < digits.Length; i++)
            {
                if (digits[i] == ch)
                    len++;
                else
                {
                    runs.Add((ch, len));
                    ch = digits[i];
                    len = 1;
                }
            }

            runs.Add((ch, len));
            return runs;
        }

        private static int GetLongestSameDigitRun(string digits)
        {
            int max = 1;
            int run = 1;

            for (int i = 1; i < digits.Length; i++)
            {
                if (digits[i] == digits[i - 1])
                {
                    run++;
                    max = Math.Max(max, run);
                }
                else
                {
                    run = 1;
                }
            }

            return max;
        }

        private static string TrimTrailingDigitNoise(string digits)
        {
            if (digits.Length >= 6 && digits.EndsWith("10", StringComparison.Ordinal))
                digits = digits[..^2];
            else if (digits.Length == 5 && digits.EndsWith('0') && digits[..^1].All(c => c == digits[0]))
                digits = digits[..^1];

            while (digits.Length > MaxDigits + 1
                   && (digits[^1] == '1' || digits[^1] == '2'))
            {
                digits = digits[..^1];
            }

            return digits;
        }

        private static bool IsThaiConsonant(char c) =>
            c >= '\u0E01' && c <= '\u0E2E';

        private static bool IsThaiDigit(char c) =>
            c >= '\u0E50' && c <= '\u0E59';
    }
}
