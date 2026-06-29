using OpenCvSharp;

namespace ConsoleApp1
{
    /// <summary>ภาพป้ายที่ส่งเข้า OCR (หลัง prepare) สำหรับ debug บนจอ</summary>
    public sealed class PlateOcrPreview : System.IDisposable
    {
        public Mat Image { get; }
        public string PlateNumber { get; init; } = string.Empty;
        public string Province { get; init; } = string.Empty;
        public float DetectConfidence { get; init; }

        public PlateOcrPreview(Mat image, string plateNumber, string province, float detectConfidence)
        {
            Image = image.Clone();
            PlateNumber = plateNumber;
            Province = province;
            DetectConfidence = detectConfidence;
        }

        public void Dispose()
        {
            if (!Image.IsDisposed)
                Image.Dispose();
        }
    }
}
