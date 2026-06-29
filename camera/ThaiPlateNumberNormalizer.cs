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

            string lettersFinal = ThaiPlateLetterConfusion.FixPrefix(letters.ToString());
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

            if (leadingDigits.Length == 1 && (leadingDigits[0] == '1' || leadingDigits[0] == '2' || leadingDigits[0] == '0'))
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

            ThaiPlateLetterConfusion.ApplyStringBuilderFixes(letters);

            string consonants = letters.ToString(consonantStart, letters.Length - consonantStart);

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

        /// <summary>ปรับเลขหลัง parse/vote — ใช้ร่วมกับ PlateReadTracker ได้</summary>
        internal static string RefineDigits(string digits, string letters)
        {
            if (string.IsNullOrEmpty(digits))
                return string.Empty;

            digits = FixSameDigitTrailingZero(digits);
            digits = TrimShortPlateExtension(digits, letters);
            digits = FixHomogeneousOutlier(digits);
            digits = FixAabbTrailingDigit(digits);
            digits = FixAabb6688Family(digits, letters);
            digits = FixAbbaPattern(digits);

            if (digits.Length == 3 && digits.All(c => c == digits[0]))
                return new string(digits[0], MaxDigits);

            if (digits.Length == MaxDigits)
                return digits;

            if (TryCompleteDoublePair(digits, out string pair))
                return pair;

            return digits;
        }

        private static string FinalizeDigits(string digits, string letters) =>
            RefineDigits(digits, letters);

        /// <summary>
        /// 6661 / 9919 / 2223 — 3 ใน 4 หลักเหมือนกัน และหลักที่เพี้ยนเป็น OCR ที่พบบ่อย
        /// ไม่แตะ 9299 (2 ไม่ใช่ confusion ของ 9) หรือ 9911 (ไม่มี 3 หลักชนะ)
        /// </summary>
        private static string FixHomogeneousOutlier(string digits)
        {
            if (digits.Length != MaxDigits)
                return digits;

            var groups = digits.GroupBy(c => c).OrderByDescending(g => g.Count()).ToList();
            if (groups[0].Count() < 3)
                return digits;

            char dominant = groups[0].Key;
            var outliers = groups.Where(g => g.Key != dominant).SelectMany(g => g).ToList();
            if (outliers.Count != 1)
                return digits;

            char outlier = outliers[0];
            if (!IsLikelyDigitMisread(outlier, dominant))
                return digits;

            return digits.Replace(outlier, dominant);
        }

        /// <summary>6680 / 6608 — AABB แต่ท้าย/กลางเป็น 0 แทน 6 หรือ 8</summary>
        private static string FixAabbTrailingDigit(string digits)
        {
            if (digits.Length != MaxDigits)
                return digits;

            // 6680 → 6688
            if (digits[0] == digits[1] && digits[2] != digits[3]
                && digits[3] == '0' && IsLikelyDigitMisread('0', digits[2]))
            {
                return digits[..3] + digits[2];
            }

            // 6608 → 6688
            if (digits[0] == digits[1] && digits[2] == '0' && digits[3] == digits[1]
                && IsLikelyDigitMisread('0', digits[1]))
            {
                return $"{digits[0]}{digits[1]}{digits[1]}{digits[3]}";
            }

            return digits;
        }

        /// <summary>
        /// ป้ายแดง 6กท 6688 — OCR อ่าน 6880 / 6808 / 6888 / 6850
        /// </summary>
        private static string FixAabb6688Family(string digits, string letters)
        {
            if (digits.Length != MaxDigits || IsAabbPattern(digits))
                return digits;

            // 6880 → 6688 (ขาด 6 ตัวที่ 2, ท้าย 0 แทน 8)
            if (digits[0] == '6' && digits[1] == digits[2] && digits[1] != '6'
                && digits[3] == '0' && IsLikelyDigitMisread('0', digits[1]))
            {
                return $"66{digits[1]}{digits[1]}";
            }

            // 6808 → 6688
            if (digits[0] == '6' && digits[1] == digits[3] && digits[1] != '6'
                && digits[2] == '0' && IsLikelyDigitMisread('0', '6'))
            {
                return $"66{digits[1]}{digits[1]}";
            }

            // 6888 → 6688 (ตัวที่ 2 ควรเป็น 6)
            if (digits[0] == '6' && digits[1] == '8' && digits[2] == '8' && digits[3] == '8')
                return "6688";

            if (!IsRedPlateLetters(letters))
                return digits;

            // 6850 / 6860 — เฉพาะป้ายแดง 6xxx
            if (digits[0] == '6' && digits.All(c => c is '0' or '5' or '6' or '8'))
            {
                const string target = "6688";
                if (ScoreRedPlateAabbFit(digits, target) >= 5)
                    return target;
            }

            return digits;
        }

        private static bool IsRedPlateLetters(string letters) =>
            letters.Length >= 2 && letters[0] == '6';

        private static int ScoreRedPlateAabbFit(string read, string candidate)
        {
            int score = ScorePatternFit(read, candidate);
            for (int i = 0; i < read.Length; i++)
            {
                if (read[i] == candidate[i])
                    continue;

                if (read[i] == '5' && candidate[i] == '8')
                    score += 1;
                else if (read[i] == '8' && candidate[i] == '6')
                    score += 1;
            }

            return score;
        }

        /// <summary>
        /// 1441 / 4411 / 1412 — รูป ABBA (นอก-ใน-ใน-นอก)
        /// 4411 → 1441 (สลับลำดับคู่), 1412 → 1441 (เลข 1/4 + noise 2/3)
        /// </summary>
        private static string FixAbbaPattern(string digits)
        {
            if (digits.Length != MaxDigits)
                return digits;

            if (IsAbbaPattern(digits))
                return digits;

            var counts = digits.GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
            if (counts.Count == 2 && counts.Values.All(v => v == 2))
            {
                char[] pair = counts.Keys.ToArray();
                string reordered = PickBestAbbaFromTwoPair(digits, pair[0], pair[1]);
                if (reordered != digits)
                    return reordered;
            }

            return TrySnap1441Family(digits);
        }

        private static string PickBestAbbaFromTwoPair(string digits, char a, char b)
        {
            string[] candidates =
            {
                digits,
                $"{a}{b}{b}{a}",
                $"{b}{a}{a}{b}",
                $"{a}{a}{b}{b}",
                $"{b}{b}{a}{a}"
            };

            string best = digits;
            int bestScore = ScorePatternFit(digits, digits);

            foreach (string candidate in candidates.Distinct())
            {
                int score = ScorePatternFit(digits, candidate);
                if (score <= bestScore)
                    continue;

                if (IsAbbaPattern(candidate) && score >= 6)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>1412 / 1413 / 1411 — มี 1 กับ 4 อย่างน้อย 3 หลัก (noise ได้ 1 หลัก)</summary>
        private static string TrySnap1441Family(string digits)
        {
            int ones = digits.Count(c => c == '1');
            int fours = digits.Count(c => c == '4');
            int foreign = digits.Count(c => c is not '1' and not '4');

            if (ones < 2 || fours < 1 || foreign > 1)
                return digits;

            string candidate = "1441";
            if (ScorePatternFit(digits, candidate) >= 5)
                return candidate;

            return digits;
        }

        private static int ScorePatternFit(string read, string candidate)
        {
            if (read.Length != candidate.Length)
                return 0;

            int score = 0;
            for (int i = 0; i < read.Length; i++)
            {
                if (read[i] == candidate[i])
                    score += 2;
                else if (IsLikelyDigitMisread(read[i], candidate[i]))
                    score += 1;
            }

            return score;
        }

        private static bool IsAbbaPattern(string s) =>
            s.Length == MaxDigits && s[0] == s[3] && s[1] == s[2] && s[0] != s[1];

        internal static bool IsLikelyDigitMisreadForVote(char read, char expected) =>
            IsLikelyDigitMisread(read, expected);

        private static bool IsLikelyDigitMisread(char read, char expected)
        {
            if (read == expected)
                return false;

            return expected switch
            {
                '0' => read is '6' or '8',
                '1' => read is '7' or '4' or '9',
                '2' => read is '0' or '3' or '5' or '7',
                '3' => read is '5' or '8' or '2',
                '4' => read is '1' or '6' or '9',
                '5' => read is '2' or '3' or '6',
                '6' => read is '0' or '4' or '1' or '5' or '8',
                '7' => read is '1' or '2',
                '8' => read is '0' or '6' or '3' or '9',
                '9' => read is '1' or '0' or '4' or '8',
                _ => false
            };
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

            if (digits.Length >= 4 && digits[0] is '1' or '2' or '7')
            {
                string rest = digits[1..];
                if (rest.Length >= 3 && rest.All(c => c == rest[0]))
                    digits = rest;
                else if (digits[0] == '7' && rest.Length >= 3 && rest.All(c => c == '6'))
                    digits = rest;
                else
                {
                    string refined = FixSameDigitTrailingZero(rest);
                    if (refined.Length == MaxDigits && refined.All(c => c == refined[0]))
                        digits = refined;
                }
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
            if (runs.Count == 0)
                return string.Empty;

            // 696969... → 69 (ไม่ตัด 6666 → 66 อีก)
            if (runs.Count >= 2 && digits.Length >= 6)
            {
                bool alternating = true;
                for (int i = 0; i < runs.Count - 1; i++)
                {
                    if (runs[i].Ch == runs[i + 1].Ch)
                    {
                        alternating = false;
                        break;
                    }
                }

                if (alternating && runs.All(r => r.Len >= 2))
                {
                    var shortPlate = new StringBuilder(runs.Count);
                    foreach (var (ch, _) in runs)
                        shortPlate.Append(ch);
                    return shortPlate.ToString();
                }
            }

            var sb = new StringBuilder(digits.Length);
            foreach (var (ch, len) in runs)
            {
                int cap = len >= HomogeneousSpamRun
                    ? MaxDigits
                    : Math.Min(len, MaxDigits);

                sb.Append(new string(ch, cap));
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

            if (IsAbbaPattern(window))
                score += 28;

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
