using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;

namespace ConsoleApp1
{
    /// <summary>รวม OCR หลายเฟรมต่อป้าย — exact vote, log เมื่อมั่นใจ</summary>
    internal sealed class PlateReadTracker
    {
        private const float MatchIou = 0.35f;
        private const int MaxTracks = 8;
        private const int MaxSamplesPerTrack = 32;
        private const int MaxMissFrames = 14;
        private const int MinConfirmVotes = 4;
        private const double MinConfirmRatio = 0.52;

        private readonly List<Track> _tracks = new();

        public PlateTrackUpdate Update(Rect box, string plate, string province, float detectConfidence)
        {
            if (string.IsNullOrWhiteSpace(plate) && string.IsNullOrWhiteSpace(province))
                return PlateTrackUpdate.None(plate, province);

            var track = FindOrCreateTrack(box);
            track.Box = box;
            track.MissCount = 0;
            track.Samples.Add(new Sample(
                plate?.Trim() ?? string.Empty,
                province?.Trim() ?? string.Empty,
                Math.Max(0.05f, detectConfidence),
                DateTime.UtcNow));

            if (track.Samples.Count > MaxSamplesPerTrack)
                track.Samples.RemoveAt(0);

            PruneTracks();

            if (!TryPickConfirmed(track.Samples, out string confirmedPlate, out string confirmedProvince,
                    out int plateVotes, out int sampleCount))
            {
                string provisionalPlate = PickLeadingPlate(track.Samples, plate);
                string provisionalProvince = PickLeadingProvince(track.Samples, provisionalPlate, province);
                return new PlateTrackUpdate(
                    DisplayPlate: provisionalPlate,
                    DisplayProvince: provisionalProvince,
                    ShouldLog: false,
                    LogPlate: string.Empty,
                    LogProvince: string.Empty,
                    LogConfidence: detectConfidence,
                    ConfirmVotes: 0,
                    SampleCount: sampleCount);
            }

            string logKey = BuildLogKey(confirmedPlate, confirmedProvince);
            bool shouldLog = logKey != track.LastLoggedKey;

            if (shouldLog)
                track.LastLoggedKey = logKey;

            float avgConfidence = track.Samples
                .Where(s => s.Plate == confirmedPlate)
                .Select(s => s.DetectConfidence)
                .DefaultIfEmpty(detectConfidence)
                .Average();

            return new PlateTrackUpdate(
                DisplayPlate: confirmedPlate,
                DisplayProvince: confirmedProvince,
                ShouldLog: shouldLog,
                LogPlate: confirmedPlate,
                LogProvince: confirmedProvince,
                LogConfidence: avgConfidence,
                ConfirmVotes: plateVotes,
                SampleCount: sampleCount);
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

        private static bool TryPickConfirmed(
            List<Sample> samples,
            out string plate,
            out string province,
            out int plateVotes,
            out int sampleCount)
        {
            plate = string.Empty;
            province = string.Empty;
            plateVotes = 0;
            sampleCount = samples.Count;

            var plateGroups = samples
                .Where(s => !string.IsNullOrWhiteSpace(s.Plate))
                .GroupBy(s => s.Plate)
                .OrderByDescending(g => g.Count())
                .ThenByDescending(g => g.Average(s => s.DetectConfidence))
                .ToList();

            if (plateGroups.Count == 0)
                return false;

            var winner = plateGroups[0];
            plateVotes = winner.Count();
            int secondVotes = plateGroups.Count > 1 ? plateGroups[1].Count() : 0;

            if (plateVotes < MinConfirmVotes)
                return false;

            if (plateVotes < sampleCount * MinConfirmRatio)
                return false;

            if (plateVotes - secondVotes < 1 && plateGroups.Count > 1)
                return false;

            plate = winner.Key;

            var provinceGroups = winner
                .Where(s => !string.IsNullOrWhiteSpace(s.Province))
                .GroupBy(s => s.Province)
                .OrderByDescending(g => g.Count())
                .ToList();

            province = provinceGroups.Count > 0 ? provinceGroups[0].Key : string.Empty;
            return true;
        }

        private static string PickLeadingPlate(List<Sample> samples, string fallback)
        {
            var winner = samples
                .Where(s => !string.IsNullOrWhiteSpace(s.Plate))
                .GroupBy(s => s.Plate)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            if (winner == null || winner.Count() < 2)
                return fallback;

            return winner.Key;
        }

        private static string PickLeadingProvince(List<Sample> samples, string plate, string fallback)
        {
            var winner = samples
                .Where(s => s.Plate == plate && !string.IsNullOrWhiteSpace(s.Province))
                .GroupBy(s => s.Province)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            if (winner == null || winner.Count() < 2)
                return fallback;

            return winner.Key;
        }

        private static string BuildLogKey(string plate, string province) =>
            string.IsNullOrWhiteSpace(province) ? plate : $"{plate}|{province}";

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
            public string LastLoggedKey = string.Empty;
            public List<Sample> Samples { get; } = new();
        }

        private readonly record struct Sample(
            string Plate,
            string Province,
            float DetectConfidence,
            DateTime Time);
    }

    internal readonly record struct PlateTrackUpdate(
        string DisplayPlate,
        string DisplayProvince,
        bool ShouldLog,
        string LogPlate,
        string LogProvince,
        float LogConfidence,
        int ConfirmVotes,
        int SampleCount)
    {
        public static PlateTrackUpdate None(string plate, string province) =>
            new(plate, province, false, string.Empty, string.Empty, 0f, 0, 0);
    }
}
