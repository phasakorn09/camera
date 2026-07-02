using Microsoft.ML.OnnxRuntime;

namespace ConsoleApp1
{
    /// <summary>
    /// สร้าง ONNX Runtime session บน GPU (DirectML) พร้อม fallback เป็น CPU
    /// </summary>
    internal static class OnnxSessionFactory
    {
        private static bool _providerLogged;

        public static InferenceSession Create(string modelPath)
        {
            var options = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
            };

            try
            {
                options.AppendExecutionProvider_DML(0);
                if (!_providerLogged)
                {
                    Console.WriteLine("[ONNX] ใช้ DirectML (GPU device 0)");
                    _providerLogged = true;
                }
            }
            catch (Exception ex)
            {
                if (!_providerLogged)
                {
                    Console.WriteLine($"[ONNX] DirectML ไม่พร้อม — fallback CPU: {ex.Message}");
                    _providerLogged = true;
                }
            }

            return new InferenceSession(modelPath, options);
        }
    }
}
