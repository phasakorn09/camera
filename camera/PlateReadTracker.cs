using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;

namespace ConsoleApp1
{
    /// <summary>รวมผล OCR หลายเฟรม — vote ตาม prefix + ตำแหน่งเลข</summary>
    internal sealed class PlateReadTracker
    {
        private const float MatchIou = 0.35f;
        private const int MaxTracks = 8;
        private const int MaxSamplesPerTrack = 36;
        private const int MinVotes = 4;
        private const int MaxMissFrames = 12;

        private readonly List<Track> _tracks = new();

        public (string PlateNumber, string Province) Stabilize(Rect box, string plate, string province, float detectConfidence)
        {
            if (string.IsNullOrWhiteSpace(plate))
                return (plate, province);

            var track = FindOrCreateTrack(box);
            track.Box = box;
            track.MissCount = 0;
            track.Samples.Add(new Sample(plate, province, detectConfidence, DateTime.UtcNow));

            if (track.Samples.Count > MaxSamplesPerTrack)
                track.Samples.RemoveAt(0);

            PruneTracks();
            return Vote(track.Samples, fallbackPlate: plate, fallbackProvince: province);
        }

        public void MarkMissed(IReadOnlyList<Rect> activeBoxes)
        {
            foreach (var track in _tracks)
            {
                bool matched = activeBoxes.Any(box => CalculateIoU(track.Box, box) >= MatchIou);
                if (!matched)
                    track.MissCount++;
            }

            _tracks.RemoveAll(t => t.MissCount > MaxMissFrames);
        }

        private Track FindOrCreateTrack(Rect box)
        {
            Track? best = null;
            float bestIou = MatchIou;

            foreach (var track in _tracks)
            {
                float iou = CalculateIoU(track.Box, box);
                if (iou > bestIou)
                {
                    bestIou = iou;
                    best = track;
                }
            }

            if (best != null)
                return best;

            var created = new Track { Box = box };
            _tracks.Add(created);

            if (_tracks.Count > MaxTracks)
                _tracks.RemoveAt(0);

            return created;
        }

        private static (string PlateNumber, string Province) Vote(
            List<Sample> samples,
            string fallbackPlate,
            string fallbackProvince)
        {
            if (samples.Count < MinVotes)
                return (fallbackPlate, fallbackProvince);

            string? prefixWinner = ThaiPlateLetterConfusion.VotePrefix(
                samples.Select(s => (
                    ParsePlate(s.Plate).Letters,
                    Math.Max(0.05f, s.DetectConfidence))));

            if (string.IsNullOrEmpty(prefixWinner))
                return VoteExactPlate(samples, fallbackPlate, fallbackProvince);

            var related = samples
                .Where(s => ThaiPlateLetterConfusion.PrefixMatches(ParsePlate(s.Plate).Letters, prefixWinner))
                .ToList();

            string votedDigits = VoteDigitsPositionally(related, prefixWinner);
            votedDigits = ThaiPlateNumberNormalizer.RefineDigits(votedDigits, prefixWinner);
            votedDigits = TrimVotedDigitLength(prefixWinner, votedDigits);
            string votedPlate = string.IsNullOrEmpty(votedDigits)
                ? fallbackPlate
                : $"{prefixWinner} {votedDigits}";

            var provinceWinner = related
                .Where(s => !string.IsNullOrWhiteSpace(s.Province))
                .GroupBy(s => s.Province)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            string province = provinceWinner != null && provinceWinner.Count() >= MinVotes
                ? provinceWinner.Key
                : fallbackProvince;

            return (votedPlate, province);
        }

        private static (string PlateNumber, string Province) VoteExactPlate(
            List<Sample> samples,
            string fallbackPlate,
            string fallbackProvince)
        {
            var plateWinner = samples
                .GroupBy(s => s.Plate)
                .OrderByDescending(g => g.Count())
                .ThenByDescending(g => DigitDiversity(g.Key))
                .FirstOrDefault();

            string plate = plateWinner != null && plateWinner.Count() >= MinVotes
                ? plateWinner.Key
                : fallbackPlate;

            var provinceWinner = samples
                .Where(s => s.Plate == plate && !string.IsNullOrWhiteSpace(s.Province))
                .GroupBy(s => s.Province)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            string province = provinceWinner != null && provinceWinner.Count() >= MinVotes
                ? provinceWinner.Key
                : fallbackProvince;

            return (plate, province);
        }

        /// <summary>vote ทีละหลักจากขวา — เลขท้ายเสถียรกว่า; ถ่วงน้ำหนัก detect confidence</summary>
        private static string VoteDigitsPositionally(List<Sample> samples, string letters)
        {
            var digitSamples = samples
                .Select(s => (Digits: ParsePlate(s.Plate).Digits, Weight: Math.Max(0.05f, s.DetectConfidence)))
                .Where(x => x.Digits.Length > 0)
                .ToList();

            if (digitSamples.Count == 0)
                return string.Empty;

            int targetLen = VoteTargetDigitLength(digitSamples, letters);
            if (targetLen <= 0)
                return string.Empty;

            var sb = new System.Text.StringBuilder(targetLen);
            for (int posFromRight = 0; posFromRight < targetLen; posFromRight++)
            {
                var votes = new Dictionary<char, float>();
                foreach (var (digits, weight) in digitSamples)
                {
                    int idx = digits.Length - 1 - posFromRight;
                    if (idx < 0)
                        continue;

                    char c = digits[idx];
                    votes[c] = votes.GetValueOrDefault(c) + weight;

                    // 1↔4 สลับกันบ่อยบนป้าย ABBA เช่น 1441
                    if (c == '1' || c == '4')
                    {
                        char alt = c == '1' ? '4' : '1';
                        votes[alt] = votes.GetValueOrDefault(alt) + weight * 0.35f;
                    }

                    // ป้ายแดง 6688 — 6 กับ 8 สลับตำแหน่งบ่อย
                    if (letters.StartsWith("6") && letters.Length >= 2 && (c == '6' || c == '8'))
                    {
                        int posFromLeft = targetLen - 1 - posFromRight;
                        char expected = posFromLeft <= 1 ? '6' : '8';
                        if (c != expected)
                            votes[expected] = votes.GetValueOrDefault(expected) + weight * 0.45f;
                    }
                }

                if (votes.Count == 0)
                    continue;

                char winner = PickWeightedDigitWinner(votes);
                sb.Insert(0, winner);
            }

            return sb.ToString();
        }

        private static int VoteTargetDigitLength(
            List<(string Digits, float Weight)> digitSamples,
            string letters)
        {
            if (letters == "ฐฐ")
                return 2;

            var lengthVotes = new Dictionary<int, float>();
            foreach (var (digits, weight) in digitSamples)
            {
                int len = Math.Min(digits.Length, 4);
                lengthVotes[len] = lengthVotes.GetValueOrDefault(len) + weight;
            }

            int bestLen = lengthVotes
                .OrderByDescending(kv => kv.Value)
                .ThenByDescending(kv => kv.Key)
                .First().Key;

            return Math.Clamp(bestLen, 1, 4);
        }

        private static char PickWeightedDigitWinner(Dictionary<char, float> votes) =>
            votes.OrderByDescending(kv => kv.Value).First().Key;

        /// <summary>ฐฐ → 69 ไม่ใช่ 6933</summary>
        private static string TrimVotedDigitLength(string letters, string voted)
        {
            if (string.IsNullOrEmpty(voted))
                return voted;

            if (letters == "ฐฐ" && voted.StartsWith("69", StringComparison.Ordinal) && voted.Length > 2)
                return voted[..2];

            return voted;
        }

        private static (string Letters, string Digits) ParsePlate(string plate)
        {
            if (string.IsNullOrWhiteSpace(plate))
                return (string.Empty, string.Empty);

            plate = plate.Trim();
            int space = plate.IndexOf(' ');
            if (space > 0)
                return (plate[..space].Trim(), plate[(space + 1)..].Trim());

            var letters = new System.Text.StringBuilder();
            var digits = new System.Text.StringBuilder();

            foreach (char c in plate)
            {
                if (c >= '0' && c <= '9')
                    digits.Append(c);
                else
                    letters.Append(c);
            }

            return (letters.ToString(), digits.ToString());
        }

        private static int DigitDiversity(string plate)
        {
            return ParsePlate(plate).Digits.Distinct().Count();
        }

        private void PruneTracks()
        {
            _tracks.RemoveAll(t => t.MissCount > MaxMissFrames);
        }

        private static float CalculateIoU(Rect a, Rect b)
        {
            int x1 = Math.Max(a.X, b.X);
            int y1 = Math.Max(a.Y, b.Y);
            int x2 = Math.Min(a.X + a.Width, b.X + b.Width);
            int y2 = Math.Min(a.Y + a.Height, b.Y + b.Height);

            int interW = Math.Max(0, x2 - x1);
            int interH = Math.Max(0, y2 - y1);
            int interArea = interW * interH;
            int unionArea = a.Width * a.Height + b.Width * b.Height - interArea;

            return unionArea <= 0 ? 0f : (float)interArea / unionArea;
        }

        private sealed class Track
        {
            public Rect Box;
            public int MissCount;
            public List<Sample> Samples { get; } = new();
        }

        private readonly record struct Sample(string Plate, string Province, float DetectConfidence, DateTime Time);
    }
}
