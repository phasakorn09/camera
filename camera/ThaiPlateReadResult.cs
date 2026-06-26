namespace ConsoleApp1
{
    /// <summary>ผล OCR ป้ายทะเบียนไทย 2 บรรทัด</summary>
    public class ThaiPlateReadResult
    {
        public string PlateNumber { get; init; } = string.Empty;
        public string Province { get; init; } = string.Empty;

        public string FullText
        {
            get
            {
                if (string.IsNullOrWhiteSpace(PlateNumber) && string.IsNullOrWhiteSpace(Province))
                    return string.Empty;
                if (string.IsNullOrWhiteSpace(Province))
                    return PlateNumber.Trim();
                if (string.IsNullOrWhiteSpace(PlateNumber))
                    return Province.Trim();
                return $"{PlateNumber.Trim()} | {Province.Trim()}";
            }
        }

        public bool HasAnyText =>
            !string.IsNullOrWhiteSpace(PlateNumber) || !string.IsNullOrWhiteSpace(Province);
    }
}
