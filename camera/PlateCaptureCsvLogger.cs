using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ConsoleApp1
{
    /// <summary>บันทึกผล OCR ลง CSV — audit / debug ย้อนหลัง</summary>
    internal sealed class PlateCaptureCsvLogger
    {
        private readonly string _csvPath;
        private readonly object _writeLock = new();

        public PlateCaptureCsvLogger(string capturesDirectory)
        {
            Directory.CreateDirectory(capturesDirectory);
            _csvPath = Path.Combine(capturesDirectory, "plates.csv");
            EnsureHeader();
        }

        public string CsvPath => _csvPath;

        public void Append(in PlateCaptureResult capture)
        {
            lock (_writeLock)
            {
                EnsureHeader();

                string line = string.Join(",",
                    Escape(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)),
                    Escape(capture.PlateNumber),
                    Escape(capture.Province),
                    capture.DetectConfidence.ToString("F2", CultureInfo.InvariantCulture),
                    capture.SharpnessScore.ToString("F1", CultureInfo.InvariantCulture),
                    capture.FramesCollected.ToString(CultureInfo.InvariantCulture),
                    capture.IsValidated ? "1" : "0",
                    capture.ShouldLog ? "1" : "0",
                    Escape(capture.SavedImagePath));

                File.AppendAllText(_csvPath, line + Environment.NewLine, Encoding.UTF8);
            }
        }

        private void EnsureHeader()
        {
            if (File.Exists(_csvPath))
                return;

            const string header =
                "timestamp,plate,province,detect_conf,sharp,frames,validated,logged,image_path";
            File.WriteAllText(_csvPath, header + Environment.NewLine, Encoding.UTF8);
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
                return $"\"{value.Replace("\"", "\"\"")}\"";

            return value;
        }
    }
}
