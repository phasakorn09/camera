using System;
using System.Linq;

namespace ConsoleApp1
{
    /// <summary>
    /// ชุดอักขระที่อนุญาตบนป้ายไทย: พยัญชนะ 44 ตัว, เลข 0–9 (ค่า 1–9999), จังหวัด 77 แห่ง
    /// </summary>
    internal static class ThaiPlateCharset
    {
        public const string Consonants =
            "กขฃคฅฆงจฉชซฌญฎฏฐฑฒณดตถทธนบปผฝพฟภมยรลวศษสหฬอฮ";

        public static readonly string[] Provinces =
        {
            "กรุงเทพมหานคร",
            "กระบี่",
            "กาญจนบุรี",
            "กาฬสินธุ์",
            "กำแพงเพชร",
            "ขอนแก่น",
            "จันทบุรี",
            "ฉะเชิงเทรา",
            "ชลบุรี",
            "ชัยนาท",
            "ชัยภูมิ",
            "ชุมพร",
            "เชียงราย",
            "เชียงใหม่",
            "ตรัง",
            "ตราด",
            "ตาก",
            "นครนายก",
            "นครปฐม",
            "นครพนม",
            "นครราชสีมา",
            "นครศรีธรรมราช",
            "นครสวรรค์",
            "นนทบุรี",
            "นราธิวาส",
            "น่าน",
            "บึงกาฬ",
            "บุรีรัมย์",
            "ปทุมธานี",
            "ประจวบคีรีขันธ์",
            "ปราจีนบุรี",
            "ปัตตานี",
            "พระนครศรีอยุธยา",
            "พังงา",
            "พัทลุง",
            "พิจิตร",
            "พิษณุโลก",
            "เพชรบุรี",
            "เพชรบูรณ์",
            "แพร่",
            "พะเยา",
            "ภูเก็ต",
            "มหาสารคาม",
            "มุกดาหาร",
            "แม่ฮ่องสอน",
            "ยโสธร",
            "ยะลา",
            "ร้อยเอ็ด",
            "ระนอง",
            "ระยอง",
            "ราชบุรี",
            "ลพบุรี",
            "ลำปาง",
            "ลำพูน",
            "เลย",
            "ศรีสะเกษ",
            "สกลนคร",
            "สงขลา",
            "สตูล",
            "สมุทรปราการ",
            "สมุทรสงคราม",
            "สมุทรสาคร",
            "สระแก้ว",
            "สระบุรี",
            "สิงห์บุรี",
            "สุโขทัย",
            "สุพรรณบุรี",
            "สุราษฎร์ธานี",
            "สุรินทร์",
            "หนองคาย",
            "หนองบัวลำภู",
            "อ่างทอง",
            "อำนาจเจริญ",
            "อุดรธานี",
            "อุตรดิตถ์",
            "อุทัยธานี",
            "อุบลราชธานี"
        };

        static ThaiPlateCharset()
        {
            if (Consonants.Length != 44)
                throw new InvalidOperationException("Thai plate consonants must be the 44 official letters.");
            if (Provinces.Length != 77)
                throw new InvalidOperationException("Thai plate provinces must be all 77 jurisdictions.");
        }

        public static bool IsPlateConsonant(char c) =>
            Consonants.IndexOf(c) >= 0;

        public static bool IsPlateDigit(char c) =>
            c >= '0' && c <= '9';

        public static bool IsThaiDigit(char c) =>
            c >= '\u0E50' && c <= '\u0E59';

        public static bool IsPlateNumberChar(char c) =>
            IsPlateConsonant(c) || IsPlateDigit(c);

        public static bool IsProvinceChar(char c)
        {
            if (IsPlateConsonant(c)) return true;
            if (c >= '\u0E30' && c <= '\u0E3A') return true;
            if (c >= '\u0E40' && c <= '\u0E4E') return true;
            return false;
        }

        public static bool IsValidPlateNumber(int value) =>
            value is >= 1 and <= 9999;

        public static bool IsValidPlateDigits(string digits)
        {
            if (string.IsNullOrEmpty(digits) || digits.Length > 4 || !digits.All(IsPlateDigit))
                return false;
            return int.TryParse(digits, out int value) && IsValidPlateNumber(value);
        }
    }
}
