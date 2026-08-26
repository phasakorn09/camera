using System;
using System.Collections.Generic;
using OpenCvSharp;

namespace ConsoleApp1
{
    /// <summary>
    /// เปิดแหล่งวิดีโอ — ค่าเริ่มต้นคือ webcam โน้ตบุ๊ก
    /// กล้อง IP ใช้เมื่อส่ง --url / --ip เท่านั้น
    /// </summary>
    internal static class CameraSource
    {
        public const string DefaultIp = "192.168.254.6";

        public static VideoCapture Open(string[] args, out string sourceDescription)
        {
            Environment.SetEnvironmentVariable(
                "OPENCV_FFMPEG_CAPTURE_OPTIONS",
                "rtsp_transport;tcp|fflags;nobuffer|flags;low_delay|max_delay;0");

            bool forceIp = !string.IsNullOrWhiteSpace(GetOption(args, "--url"))
                || !string.IsNullOrWhiteSpace(GetOption(args, "--ip"))
                || HasFlag(args, "--rtsp")
                || HasFlag(args, "--ip-camera");

            if (!forceIp || HasFlag(args, "--webcam"))
            {
                sourceDescription = "webcam โน้ตบุ๊ก";
                var webcam = OpenLaptopWebcam();
                webcam.Set(VideoCaptureProperties.BufferSize, 1);
                return webcam;
            }

            foreach (string url in BuildCandidateUrls(args))
            {
                var capture = new VideoCapture(url);
                capture.Set(VideoCaptureProperties.BufferSize, 1);
                if (capture.IsOpened())
                {
                    sourceDescription = RedactUrl(url);
                    return capture;
                }

                capture.Dispose();
            }

            sourceDescription = $"IP camera {DefaultIp} (unreachable)";
            return new VideoCapture();
        }

        public static IReadOnlyList<string> BuildCandidateUrls(string[] args)
        {
            var urls = new List<string>();

            string? cliUrl = GetOption(args, "--url");
            if (!string.IsNullOrWhiteSpace(cliUrl))
                urls.Add(cliUrl.Trim());

            string? envUrl = Environment.GetEnvironmentVariable("CAMERA_URL");
            if (!string.IsNullOrWhiteSpace(envUrl) && !urls.Contains(envUrl.Trim()))
                urls.Add(envUrl.Trim());

            string ip = FirstNonEmpty(
                GetOption(args, "--ip"),
                Environment.GetEnvironmentVariable("CAMERA_IP"),
                DefaultIp);

            string user = FirstNonEmpty(
                GetOption(args, "--user"),
                Environment.GetEnvironmentVariable("CAMERA_USER"),
                "admin");

            string password = FirstNonEmpty(
                GetOption(args, "--password"),
                Environment.GetEnvironmentVariable("CAMERA_PASSWORD"),
                "Admin1234");

            urls.Add($"rtsp://{user}:{password}@{ip}:554/Streaming/Channels/101");
            urls.Add($"rtsp://{user}:{password}@{ip}:554/Streaming/Channels/102");
            urls.Add($"rtsp://{user}:{password}@{ip}:554/h264/ch1/main/av_stream");

            return urls;
        }

        private static VideoCapture OpenLaptopWebcam()
        {
            foreach (int index in new[] { 0, 1 })
            {
                var webcam = new VideoCapture(index);
                webcam.Set(VideoCaptureProperties.BufferSize, 1);
                if (webcam.IsOpened())
                    return webcam;

                webcam.Dispose();
            }

            return new VideoCapture(0);
        }

        public static bool WantsOnce(string[] args) =>
            HasFlag(args, "--once") || HasFlag(args, "--snapshot");

        public static bool HasFlag(string[] args, string flag)
        {
            foreach (string arg in args)
            {
                if (string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public static string? GetOption(string[] args, string name)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    return args[i + 1];
            }

            return null;
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            foreach (string? value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }

            return string.Empty;
        }

        internal static string RedactUrl(string url)
        {
            int schemeEnd = url.IndexOf("://", StringComparison.Ordinal);
            if (schemeEnd < 0)
                return url;

            int at = url.IndexOf('@');
            if (at < 0)
                return url;

            return url[..(schemeEnd + 3)] + "***@" + url[(at + 1)..];
        }
    }
}
