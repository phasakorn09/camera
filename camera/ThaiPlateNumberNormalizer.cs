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

        /// <summary>ตัvที่มักเป็น noise ท้าย prefix เมื่อ OCR อ่านเกิน 2 ตัv</summary>
        private static readonly HashSet<char> TrailingPrefixNoise = new()
        {
            'ร', 'น', 'ณ', 'ญ', 'ว', 'ล', 'ฤ', '์'
        };

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

            string digitsRaw = ExtractBestDigits(new string(digitChars.ToArray()));
            string lettersFinal = ThaiPlateLetterConfusion.FixPrefix(letters.ToString(), digitsRaw);
            lettersFinal = TrimExcessPrefixConsonants(lettersFinal);
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

        /// <summary>บังคับ prefix ไทย 1–2 ตัv (ยกเว้น vanity เช่น ฐฐ) — ตัด OCR ที่อ่านเกิน</summary>
        private static string TrimExcessPrefixConsonants(string lettersWithPossibleLeadingDigits)
        {
            if (string.IsNullOrEmpty(lettersWithPossibleLeadingDigits))
                return lettersWithPossibleLeadingDigits;

            int digitPrefixLen = 0;
            while (digitPrefixLen < lettersWithPossibleLeadingDigits.Length
                   && char.IsDigit(lettersWithPossibleLeadingDigits[digitPrefixLen]))
                digitPrefixLen++;

            string leading = lettersWithPossibleLeadingDigits[..digitPrefixLen];
            string consonants = lettersWithPossibleLeadingDigits[digitPrefixLen..];

            if (consonants.Length <= 2)
                return lettersWithPossibleLeadingDigits;

            if (consonants == "ฐฐ" || ThaiPlateLetterConfusion.IsThoThoLikePrefix(consonants))
                return leading + "ฐฐ";

            if (consonants.Length >= 2 && consonants[0] == consonants[1])
                return leading + consonants[..2];

            while (consonants.Length > 2 && TrailingPrefixNoise.Contains(consonants[^1]))
                consonants = consonants[..^1];

            if (consonants.Length <= 2)
                return leading + consonants;

            consonants = PickBestTwoConsonantWindow(consonants);
            return leading + consonants;
        }

        private static string PickBestTwoConsonantWindow(string consonants)
        {
            if (consonants.Length <= 2)
                return consonants;

            string best = consonants[..2];
            int bestScore = ScorePrefixPair(consonants, 0);

            for (int start = 1; start <= consonants.Length - 2; start++)
            {
                int score = ScorePrefixPair(consonants, start);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = consonants.Substring(start, 2);
                }
            }

            return best;
        }

        private static int ScorePrefixPair(string consonants, int start)
        {
            char a = consonants[start];
            char b = consonants[start + 1];
            int score = 0;

            if (start == 0)
                score += 3;
            else if (start == 1 && consonants.Length >= 3)
                score += 2;

            if (a is 'ฎ' or 'ฏ' or 'ฒ')
                score += 2;

            if (TrailingPrefixNoise.Contains(b))
                score -= 3;

            if (TrailingPrefixNoise.Contains(a) && start > 0)
                score -= 2;

            if (NoiseLeadConsonants.Contains(a) && start == 0 && consonants.Length > 2)
                score -= 1;

            return score;
        }

        /// <summary>ปรับเลขหลัง parse — ใช้ร่วมกับ ThaiPlateNumberNormalizer.Normalize ได้</summary>
        internal static string RefineDigits(string digits, string letters)
        {
            if (string.IsNullOrEmpty(digits))
                return string.Empty;

            digits = FixSameDigitTrailingZero(digits);
            digits = TrimShortPlateExtension(digits, letters);
            digits = FixHomogeneousOutlier(digits);
            digits = FixVanityDoublePattern(digits, letters);

            // ไม่ขยาย 3→4 อัตโนมัติ — ป้าย 3 หลักจริng (122, 688, 888) จะไม่กลายเป็น 4 หลัก
            if (digits.Length == MaxDigits)
                return digits;

            return digits;
        }

        /// <summary>4 หลักที่น่aเป็น vanity ขยายจาก 3 หลัก OCR (688→6688, 122→1222)</summary>
        internal static bool IsLikelyVanityExpansionFromThree(string fourDigits, string threeDigits)
        {
            if (fourDigits.Length != 4 || threeDigits.Length != 3)
                return false;

            if (threeDigits[1] == threeDigits[2]
                && fourDigits == $"{threeDigits[0]}{threeDigits[0]}{threeDigits[1]}{threeDigits[1]}")
                return true;

            if (threeDigits[0] == threeDigits[1]
                && fourDigits == $"{threeDigits[0]}{threeDigits[0]}{threeDigits[2]}{threeDigits[2]}")
                return true;

            if (threeDigits.All(c => c == threeDigits[0])
                && fourDigits == new string(threeDigits[0], MaxDigits))
                return true;

            return false;
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

        private const int MinVanitySnapScore = 5;

        /// <summary>
        /// เลขเบิ้ลทั่วไป — AABB (1122), ABBA (1221), AAAA (1111)
        /// ใช้กับทุกตัวเลข ไม่ hardcode 7766/2662/6688/1441
        /// </summary>
        private static string FixVanityDoublePattern(string digits, string letters)
        {
            if (digits.Length != MaxDigits)
                return digits;

            if (IsVanityDoublePattern(digits))
                return digits;

            return SnapToBestVanityDouble(digits, letters);
        }

        private static bool IsVanityDoublePattern(string s) =>
            s.Length == MaxDigits && (IsAabbPattern(s) || IsAbbaPattern(s) || s.All(c => c == s[0]));

        private static string SnapToBestVanityDouble(string digits, string letters)
        {
            string best = digits;
            int bestScore = ScoreVanityCandidate(digits, digits, letters);

            foreach (string candidate in BuildVanityDoubleCandidates(digits))
            {
                if (!IsVanityDoublePattern(candidate))
                    continue;

                int score = ScoreVanityCandidate(digits, candidate, letters);
                if (score <= bestScore || score < MinVanitySnapScore)
                    continue;

                bestScore = score;
                best = candidate;
            }

            return best;
        }

        /// <summary>สร้าง candidate จากโครงสร้าง OCR + ชุดตัวเลขที่อ่านได้</summary>
        private static IEnumerable<string> BuildVanityDoubleCandidates(string digits)
        {
            yield return digits;

            foreach (string partial in BuildStructuralVanityFixes(digits))
                yield return partial;

            var groups = digits.GroupBy(c => c).OrderByDescending(g => g.Count()).ToList();
            char[] uniq = groups.Select(g => g.Key).ToArray();

            if (uniq.Length == 1)
            {
                yield return new string(uniq[0], MaxDigits);
                yield break;
            }

            if (uniq.Length != 2)
                yield break;

            char a = uniq[0];
            char b = uniq[1];

            if (groups[0].Count() == 2 && groups[1].Count() == 2)
            {
                yield return $"{a}{a}{b}{b}";
                yield return $"{b}{b}{a}{a}";
                yield return $"{a}{b}{b}{a}";
                yield return $"{b}{a}{a}{b}";
            }
            else if (groups[0].Count() == 3)
            {
                char dominant = groups[0].Key;
                char minor = groups[1].Key;
                yield return $"{minor}{minor}{dominant}{dominant}";
                yield return $"{dominant}{dominant}{minor}{minor}";
                yield return $"{minor}{dominant}{dominant}{minor}";
                yield return $"{dominant}{minor}{minor}{dominant}";
            }
        }

        /// <summary>แก้จากรูปทรง OCR — ใช้ได้ทุกหลัก ไม่ผูกเลขใดเลขหนึ่ง</summary>
        private static IEnumerable<string> BuildStructuralVanityFixes(string digits)
        {
            // ABBA: XY Y? → XYYX (เช่น 2660 → 2662)
            if (digits[1] == digits[2] && digits[0] != digits[3]
                && IsLikelyDigitMisread(digits[3], digits[0]))
            {
                yield return $"{digits[0]}{digits[1]}{digits[2]}{digits[0]}";
            }

            // AABB: XYYY → XXYY (เช่น 7666 → 7766)
            if (digits[1] == digits[2] && digits[2] == digits[3] && digits[0] != digits[1])
            {
                yield return $"{digits[0]}{digits[0]}{digits[1]}{digits[1]}";
            }

            // AABB: XX Y? → XXYY
            if (digits[0] == digits[1] && digits[2] != digits[3]
                && IsLikelyDigitMisread(digits[3], digits[2]))
            {
                yield return $"{digits[0]}{digits[1]}{digits[2]}{digits[2]}";
            }

            // AABB: X? YY → XXYY
            if (digits[0] != digits[1] && digits[2] == digits[3]
                && IsLikelyDigitMisread(digits[1], digits[0]))
            {
                yield return $"{digits[0]}{digits[0]}{digits[2]}{digits[3]}";
            }

            // AABB: XX Y0 → XXYY
            if (digits[0] == digits[1] && digits[2] != digits[3]
                && digits[3] == '0' && IsLikelyDigitMisread('0', digits[2]))
            {
                yield return $"{digits[0]}{digits[1]}{digits[2]}{digits[2]}";
            }

            // AABB: XX 0Y → XXYY
            if (digits[0] == digits[1] && digits[2] == '0' && digits[3] == digits[1]
                && IsLikelyDigitMisread('0', digits[1]))
            {
                yield return $"{digits[0]}{digits[1]}{digits[1]}{digits[3]}";
            }

            // AABB: X YYY → XXYY (6888 → 6688)
            if (digits[0] != digits[1] && digits[1] == digits[2] && digits[2] == digits[3]
                && IsLikelyDigitMisread(digits[1], digits[0]))
            {
                yield return $"{digits[0]}{digits[0]}{digits[1]}{digits[1]}";
            }
        }

        private static int ScoreVanityCandidate(string read, string candidate, string letters)
        {
            int score = ScorePatternFit(read, candidate);

            if (read[0] == candidate[0])
                score += 2;
            if (read[1] == candidate[1])
                score += 1;
            if (read[2] == candidate[2])
                score += 1;
            if (read[3] == candidate[3])
                score += 2;

            if (IsAabbPattern(candidate))
                score += 1;

            if (IsRedPlateLetters(letters) && candidate[0] == '6' && IsVanityDoublePattern(candidate))
                score += 1;

            return score;
        }

        private static bool IsRedPlateLetters(string letters) =>
            letters.Length >= 2 && letters[0] == '6';

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
                '2' => read is '0' or '3' or '5' or '6' or '7',
                '3' => read is '5' or '8' or '2',
                '4' => read is '1' or '6' or '9',
                '5' => read is '2' or '3' or '6',
                '6' => read is '0' or '2' or '4' or '1' or '5' or '8',
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
            if (letters == "ฐฐ" || ThaiPlateLetterConfusion.IsThoThoLikePrefix(letters))
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

            if (c2 == '1')
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

            if (digits.Length >= 4 && digits[0] != digits[1])
            {
                string rest = digits[1..];
                if (rest.Length == 3 && rest.All(c => c == rest[0]) && digits[0] != rest[0])
                {
                    string vanity = $"{digits[0]}{digits[0]}{rest[0]}{rest[0]}";
                    if (IsAabbPattern(vanity))
                        return vanity;
                }
            }

            if (digits.Length >= 4 && digits[0] is '1' or '2' or '7')
            {
                string rest = digits[1..];
                if (rest.Length == 3 && rest.All(c => c == rest[0]))
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
            int bestIndex = 0;

            for (int i = 1; i <= digits.Length - MaxDigits; i++)
            {
                string window = digits.Substring(i, MaxDigits);
                int score = ScoreDigitWindow(window);
                if (score > bestScore || (score == bestScore && i < bestIndex))
                {
                    bestScore = score;
                    best = window;
                    bestIndex = i;
                }
            }

            return best;
        }

        private static int ScoreDigitWindow(string window)
        {
            if (window.Length != MaxDigits)
                return 0;

            int unique = window.Distinct().Count();
            int score = unique * 50;

            // เลขเบิ้ล AABB/ABBA/AAAA ชนะ diversity สูง
            if (IsVanityDoublePattern(window))
                score += 100;

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
