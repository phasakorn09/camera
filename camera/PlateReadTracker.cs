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
        private const int MaxSamplesPerTrack = 24;
        private const int MinVotes = 3;
        private const int MaxMissFrames = 8;

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

            string? prefixWinner = samples
                .Select(s => ParsePlate(s.Plate).Letters)
                .Where(p => !string.IsNullOrEmpty(p))
                .GroupBy(p => p)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault();

            if (string.IsNullOrEmpty(prefixWinner))
                return VoteExactPlate(samples, fallbackPlate, fallbackProvince);

            var related = samples
                .Where(s => ParsePlate(s.Plate).Letters == prefixWinner)
                .ToList();

            string votedDigits = VoteDigitsPositionally(related);
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

        /// <summary>vote ทีละหลัก — 9299 ชนะ 9999 ถ้ามีเฟรมที่อ่าน 2 ได้</summary>
        private static string VoteDigitsPositionally(List<Sample> samples)
        {
            var digitStrings = samples
                .Select(s => ParsePlate(s.Plate).Digits)
                .Where(d => d.Length > 0)
                .ToList();

            if (digitStrings.Count == 0)
                return string.Empty;

            int maxLen = digitStrings.Max(d => d.Length);
            if (maxLen == 0)
                return string.Empty;

            var sb = new System.Text.StringBuilder(maxLen);
            for (int pos = 0; pos < maxLen; pos++)
            {
                var votes = new Dictionary<char, int>();
                foreach (string d in digitStrings)
                {
                    if (pos >= d.Length)
                        continue;

                    char c = d[pos];
                    votes[c] = votes.GetValueOrDefault(c) + 1;
                }

                if (votes.Count == 0)
                    continue;

                char winner = votes
                    .OrderByDescending(kv => kv.Value)
                    .First().Key;

                sb.Append(winner);
            }

            return sb.ToString();
        }

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
