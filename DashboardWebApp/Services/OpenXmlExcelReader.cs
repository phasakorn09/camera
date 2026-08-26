using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace DashboardWebApp.Services
{
    /// <summary>
    /// Lightweight .xlsx reader (Open XML / Office Open XML).
    /// Uses only built-in .NET Framework APIs so Visual Studio can F5 without ACE OLEDB or extra NuGet packages.
    /// </summary>
    public class OpenXmlExcelReader
    {
        private static readonly XNamespace MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace PackageRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";
        private static readonly XNamespace OfficeRelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        public IList<string> GetSheetNames(string excelPath)
        {
            using (var archive = ZipFile.OpenRead(excelPath))
            {
                var workbook = LoadXml(archive, "xl/workbook.xml");
                var sheets = workbook.Root.Element(MainNs + "sheets");
                if (sheets == null)
                {
                    return new List<string>();
                }

                return sheets.Elements(MainNs + "sheet")
                    .Select(sheet => (string)sheet.Attribute("name"))
                    .Where(name => !string.IsNullOrEmpty(name))
                    .ToList();
            }
        }

        public IList<IList<string>> ReadSheet(string excelPath, string sheetName)
        {
            if (string.IsNullOrWhiteSpace(sheetName))
            {
                throw new ArgumentException("ต้องระบุชื่อชีต", "sheetName");
            }

            using (var archive = ZipFile.OpenRead(excelPath))
            {
                var sharedStrings = LoadSharedStrings(archive);
                var dateStyleIndexes = LoadDateStyleIndexes(archive);
                var worksheetPath = ResolveWorksheetPath(archive, sheetName);
                var worksheet = LoadXml(archive, worksheetPath);
                return ParseWorksheet(worksheet, sharedStrings, dateStyleIndexes);
            }
        }

        public IList<IList<string>> ReadFirstSheet(string excelPath)
        {
            var names = GetSheetNames(excelPath);
            if (names.Count == 0)
            {
                throw new InvalidOperationException("ไม่พบชีตในไฟล์ Excel");
            }

            return ReadSheet(excelPath, names[0]);
        }

        public string FindSheetName(string excelPath, params string[] candidates)
        {
            var names = GetSheetNames(excelPath);
            foreach (var candidate in candidates)
            {
                var match = names.FirstOrDefault(name =>
                    string.Equals(name, candidate, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static Dictionary<string, string> LoadWorkbookRelationships(ZipArchive archive)
        {
            var document = LoadXml(archive, "xl/_rels/workbook.xml.rels");
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var relationship in document.Root.Elements(PackageRelNs + "Relationship"))
            {
                var id = (string)relationship.Attribute("Id");
                var target = (string)relationship.Attribute("Target");
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(target))
                {
                    continue;
                }

                map[id] = NormalizePartPath(target);
            }

            return map;
        }

        private static string ResolveWorksheetPath(ZipArchive archive, string sheetName)
        {
            var workbook = LoadXml(archive, "xl/workbook.xml");
            var sheets = workbook.Root.Element(MainNs + "sheets");
            if (sheets == null)
            {
                throw new InvalidOperationException("ไฟล์ Excel ไม่มีรายการชีต");
            }

            var sheet = sheets.Elements(MainNs + "sheet")
                .FirstOrDefault(item =>
                    string.Equals((string)item.Attribute("name"), sheetName, StringComparison.OrdinalIgnoreCase));
            if (sheet == null)
            {
                throw new InvalidOperationException("ไม่พบชีตชื่อ " + sheetName);
            }

            var relationshipId = (string)sheet.Attribute(OfficeRelNs + "id");
            if (string.IsNullOrEmpty(relationshipId))
            {
                throw new InvalidOperationException("ชีต " + sheetName + " ไม่มี relationship id");
            }

            var relationships = LoadWorkbookRelationships(archive);
            string path;
            if (!relationships.TryGetValue(relationshipId, out path))
            {
                throw new InvalidOperationException("ไม่พบไฟล์ชีตสำหรับ " + sheetName);
            }

            return path;
        }

        private static IList<string> LoadSharedStrings(ZipArchive archive)
        {
            var entry = FindEntry(archive, "xl/sharedStrings.xml");
            if (entry == null)
            {
                return new List<string>();
            }

            var document = LoadXml(entry);
            var values = new List<string>();
            foreach (var item in document.Root.Elements(MainNs + "si"))
            {
                var textNodes = item.Descendants(MainNs + "t");
                values.Add(string.Concat(textNodes.Select(node => node.Value)));
            }

            return values;
        }

        private static HashSet<int> LoadDateStyleIndexes(ZipArchive archive)
        {
            var result = new HashSet<int>();
            var entry = FindEntry(archive, "xl/styles.xml");
            if (entry == null)
            {
                return result;
            }

            var document = LoadXml(entry);
            var customDateFormats = new HashSet<int>();
            var numFmts = document.Root.Element(MainNs + "numFmts");
            if (numFmts != null)
            {
                foreach (var numFmt in numFmts.Elements(MainNs + "numFmt"))
                {
                    var formatId = (int?)numFmt.Attribute("numFmtId") ?? -1;
                    var formatCode = (string)numFmt.Attribute("formatCode") ?? string.Empty;
                    if (LooksLikeDateFormat(formatCode))
                    {
                        customDateFormats.Add(formatId);
                    }
                }
            }

            var cellXfs = document.Root.Element(MainNs + "cellXfs");
            if (cellXfs == null)
            {
                return result;
            }

            var index = 0;
            foreach (var xf in cellXfs.Elements(MainNs + "xf"))
            {
                var numFmtId = (int?)xf.Attribute("numFmtId") ?? 0;
                if (IsBuiltInDateFormat(numFmtId) || customDateFormats.Contains(numFmtId))
                {
                    result.Add(index);
                }

                index++;
            }

            return result;
        }

        private static IList<IList<string>> ParseWorksheet(
            XDocument worksheet,
            IList<string> sharedStrings,
            HashSet<int> dateStyleIndexes)
        {
            var sheetData = worksheet.Root.Element(MainNs + "sheetData");
            var rows = new List<IList<string>>();
            if (sheetData == null)
            {
                return rows;
            }

            foreach (var rowElement in sheetData.Elements(MainNs + "row"))
            {
                var cells = new Dictionary<int, string>();
                var maxIndex = -1;
                foreach (var cell in rowElement.Elements(MainNs + "c"))
                {
                    var reference = (string)cell.Attribute("r");
                    var columnIndex = ColumnIndexFromReference(reference);
                    if (columnIndex < 0)
                    {
                        continue;
                    }

                    maxIndex = Math.Max(maxIndex, columnIndex);
                    cells[columnIndex] = ReadCellValue(cell, sharedStrings, dateStyleIndexes);
                }

                var values = new List<string>();
                for (var columnIndex = 0; columnIndex <= maxIndex; columnIndex++)
                {
                    string value;
                    values.Add(cells.TryGetValue(columnIndex, out value) ? value : string.Empty);
                }

                rows.Add(values);
            }

            return rows;
        }

        private static string ReadCellValue(XElement cell, IList<string> sharedStrings, HashSet<int> dateStyleIndexes)
        {
            var cellType = (string)cell.Attribute("t");
            if (string.Equals(cellType, "inlineStr", StringComparison.OrdinalIgnoreCase))
            {
                var inline = cell.Element(MainNs + "is");
                if (inline == null)
                {
                    return string.Empty;
                }

                return string.Concat(inline.Descendants(MainNs + "t").Select(node => node.Value)).Trim();
            }

            var valueElement = cell.Element(MainNs + "v");
            var raw = valueElement == null ? string.Empty : valueElement.Value;

            if (string.Equals(cellType, "s", StringComparison.OrdinalIgnoreCase))
            {
                int sharedIndex;
                if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out sharedIndex)
                    && sharedIndex >= 0
                    && sharedIndex < sharedStrings.Count)
                {
                    return (sharedStrings[sharedIndex] ?? string.Empty).Trim();
                }

                return string.Empty;
            }

            if (string.Equals(cellType, "b", StringComparison.OrdinalIgnoreCase))
            {
                return raw == "1" ? "TRUE" : "FALSE";
            }

            var styleIndex = (int?)cell.Attribute("s");
            if (styleIndex.HasValue
                && dateStyleIndexes.Contains(styleIndex.Value)
                && !string.Equals(cellType, "s", StringComparison.OrdinalIgnoreCase))
            {
                double oaDate;
                if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out oaDate)
                    && oaDate > 20000
                    && oaDate < 80000)
                {
                    return DateTime.FromOADate(oaDate).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                }
            }

            return raw.Trim();
        }

        private static int ColumnIndexFromReference(string reference)
        {
            if (string.IsNullOrEmpty(reference))
            {
                return -1;
            }

            var index = 0;
            foreach (var character in reference)
            {
                if (!char.IsLetter(character))
                {
                    break;
                }

                index = (index * 26) + (char.ToUpperInvariant(character) - 'A' + 1);
            }

            return index - 1;
        }

        private static bool IsBuiltInDateFormat(int numFmtId)
        {
            return (numFmtId >= 14 && numFmtId <= 22)
                || (numFmtId >= 27 && numFmtId <= 36)
                || (numFmtId >= 45 && numFmtId <= 47)
                || (numFmtId >= 50 && numFmtId <= 58);
        }

        private static string NormalizePartPath(string target)
        {
            var normalized = (target ?? string.Empty).Replace('\\', '/').Trim();
            if (normalized.StartsWith("/", StringComparison.Ordinal))
            {
                normalized = normalized.TrimStart('/');
            }

            if (normalized.StartsWith("xl/", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            return "xl/" + normalized.TrimStart('/');
        }

        private static bool LooksLikeDateFormat(string formatCode)
        {
            if (string.IsNullOrEmpty(formatCode))
            {
                return false;
            }

            var normalized = formatCode.ToLowerInvariant();
            if (normalized.Contains("h") && normalized.Contains("s") && !normalized.Contains("y") && !normalized.Contains("d"))
            {
                return false;
            }

            return normalized.Contains("y")
                || normalized.Contains("d")
                || normalized.Contains("m")
                || normalized.Contains("yy")
                || normalized.Contains("dd");
        }

        private static XDocument LoadXml(ZipArchive archive, string path)
        {
            var entry = FindEntry(archive, path);
            if (entry == null)
            {
                throw new FileNotFoundException("ไม่พบส่วนประกอบในไฟล์ Excel: " + path);
            }

            return LoadXml(entry);
        }

        private static XDocument LoadXml(ZipArchiveEntry entry)
        {
            using (var stream = entry.Open())
            {
                return XDocument.Load(stream);
            }
        }

        private static ZipArchiveEntry FindEntry(ZipArchive archive, string path)
        {
            var normalized = path.Replace('\\', '/');
            return archive.Entries.FirstOrDefault(entry =>
                string.Equals(entry.FullName.Replace('\\', '/'), normalized, StringComparison.OrdinalIgnoreCase));
        }
    }
}
