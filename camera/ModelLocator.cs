using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;

namespace ConsoleApp1
{
    /// <summary>
    /// หาไฟล์โมเดลตอนรันจาก Visual Studio (bin\Debug) หรือโฟลเดอร์โปรเจกต์
    /// ถ้ายังไม่มี จะดาวน์โหลดให้อัตโนมัติ
    /// </summary>
    internal static class ModelLocator
    {
        public const string DetectorFile = "plate_rtdetr.onnx";
        public const string OcrFile = "th_pp-ocrv5_mobile_rec.onnx";
        public const string DictFile = "ppocrv5_th_dict.txt";

        private const string DetectorUrl =
            "https://huggingface.co/Topurrra/rtdetr-license-plate-detection-onnx/resolve/main/plate_rtdetr.onnx";
        private const string OcrUrl =
            "https://github.com/GreatV/oar-ocr/releases/download/v0.3.0/th_pp-ocrv5_mobile_rec.onnx";

        public static string ResolveDirectory()
        {
            string? existing = FindExistingDirectory();
            if (existing != null)
            {
                CopyIntoOutput(existing);
                return Path.Combine(AppContext.BaseDirectory, "Models");
            }

            string dest = Path.Combine(AppContext.BaseDirectory, "Models");
            Directory.CreateDirectory(dest);
            DownloadMissing(dest);

            string? dictSource = FindFileInAncestors(DictFile);
            if (dictSource != null)
            {
                string dictDest = Path.Combine(dest, DictFile);
                if (!File.Exists(dictDest))
                    File.Copy(dictSource, dictDest, overwrite: true);
            }

            return dest;
        }

        private static string? FindExistingDirectory()
        {
            foreach (string dir in CandidateDirectories())
            {
                if (HasAllModelFiles(dir))
                    return dir;
            }

            return null;
        }

        private static List<string> CandidateDirectories()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<string>();

            void Offer(string? path)
            {
                if (string.IsNullOrWhiteSpace(path))
                    return;
                string full = Path.GetFullPath(path);
                if (seen.Add(full))
                    result.Add(full);
            }

            Offer(Path.Combine(AppContext.BaseDirectory, "Models"));
            Offer(Path.Combine(Directory.GetCurrentDirectory(), "Models"));

            string? cursor = AppContext.BaseDirectory;
            for (int i = 0; i < 8 && !string.IsNullOrEmpty(cursor); i++)
            {
                Offer(Path.Combine(cursor, "Models"));
                Offer(Path.Combine(cursor, "camera", "Models"));
                cursor = Directory.GetParent(cursor)?.FullName;
            }

            cursor = Directory.GetCurrentDirectory();
            for (int i = 0; i < 8 && !string.IsNullOrEmpty(cursor); i++)
            {
                Offer(Path.Combine(cursor, "Models"));
                Offer(Path.Combine(cursor, "camera", "Models"));
                cursor = Directory.GetParent(cursor)?.FullName;
            }

            return result;
        }

        private static bool HasAllModelFiles(string dir) =>
            File.Exists(Path.Combine(dir, DetectorFile))
            && File.Exists(Path.Combine(dir, OcrFile))
            && File.Exists(Path.Combine(dir, DictFile));

        private static string? FindFileInAncestors(string fileName)
        {
            foreach (string dir in CandidateDirectories())
            {
                string path = Path.Combine(dir, fileName);
                if (File.Exists(path))
                    return path;
            }

            return null;
        }

        private static void CopyIntoOutput(string sourceDir)
        {
            string dest = Path.Combine(AppContext.BaseDirectory, "Models");
            Directory.CreateDirectory(dest);
            foreach (string name in new[] { DetectorFile, OcrFile, DictFile })
            {
                string from = Path.Combine(sourceDir, name);
                string to = Path.Combine(dest, name);
                if (!File.Exists(from))
                    continue;
                if (File.Exists(to) && new FileInfo(to).Length == new FileInfo(from).Length)
                    continue;
                File.Copy(from, to, overwrite: true);
            }
        }

        private static void DownloadMissing(string destDir)
        {
            DownloadIfMissing(Path.Combine(destDir, DetectorFile), DetectorUrl);
            DownloadIfMissing(Path.Combine(destDir, OcrFile), OcrUrl);
        }

        private static void DownloadIfMissing(string destPath, string url)
        {
            if (File.Exists(destPath) && new FileInfo(destPath).Length > 1_000_000)
                return;

            Console.WriteLine($"กำลังดาวน์โหลดโมเดล: {Path.GetFileName(destPath)}");
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            using var response = client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead)
                .GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
            using var input = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
            using var output = File.Create(destPath);
            input.CopyTo(output);
            Console.WriteLine($"บันทึกแล้ว: {destPath}");
        }
    }
}
