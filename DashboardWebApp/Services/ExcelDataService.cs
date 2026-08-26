using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web;
using DashboardWebApp.Models;

namespace DashboardWebApp.Services
{
    public class ExcelDataService
    {
        private readonly OpenXmlExcelReader _reader = new OpenXmlExcelReader();

        public string DefaultExcelPath
        {
            get { return ResolveDefaultExcelPath(); }
        }

        public DashboardData LoadDashboardData()
        {
            return LoadDashboardData(ResolveDefaultExcelPath());
        }

        public DashboardData LoadDashboardData(string excelPath)
        {
            if (string.IsNullOrWhiteSpace(excelPath) || !File.Exists(excelPath))
            {
                throw new FileNotFoundException("ไม่พบไฟล์ Excel (.xlsx)", excelPath);
            }

            var data = new DashboardData
            {
                Products = new List<Product>(),
                Customers = new List<Customer>(),
                Employees = new List<Employee>(),
                Sales = new List<Sale>(),
                SourceFileName = Path.GetFileName(excelPath),
                LoadedAt = DateTime.Now
            };

            var productSheet = _reader.FindSheetName(excelPath, "สินค้า", "Products", "Product");
            var customerSheet = _reader.FindSheetName(excelPath, "ลูกค้า", "Customers", "Customer");
            var employeeSheet = _reader.FindSheetName(excelPath, "พนักงาน", "Employees", "Employee");
            var salesSheet = _reader.FindSheetName(excelPath, "การขาย", "Sales", "Sale");

            if (productSheet != null)
            {
                ParseProducts(_reader.ReadSheet(excelPath, productSheet), data.Products);
            }

            if (customerSheet != null)
            {
                ParseCustomers(_reader.ReadSheet(excelPath, customerSheet), data.Customers);
            }

            if (employeeSheet != null)
            {
                ParseEmployees(_reader.ReadSheet(excelPath, employeeSheet), data.Employees);
            }

            if (salesSheet != null)
            {
                ParseSales(_reader.ReadSheet(excelPath, salesSheet), data.Sales);
            }

            if (data.Products.Count == 0 && data.Sales.Count == 0)
            {
                var firstSheet = _reader.GetSheetNames(excelPath).FirstOrDefault();
                if (firstSheet != null)
                {
                    ParseCombinedSheet(_reader.ReadSheet(excelPath, firstSheet), data);
                }
            }

            if (data.Products.Count == 0 && data.Sales.Count == 0 && data.Customers.Count == 0 && data.Employees.Count == 0)
            {
                throw new InvalidOperationException(
                    "อ่านไฟล์ Excel ได้ แต่ไม่พบข้อมูลสินค้า/ลูกค้า/พนักงาน/การขาย — ตรวจสอบหัวตารางหรือชื่อชีต");
            }

            return data;
        }

        public static string ResolveDefaultExcelPath()
        {
            var fileName = ConfigurationManager.AppSettings["ExcelFileName"] ?? "test4.xlsx";
            if (HttpContext.Current != null)
            {
                return HttpContext.Current.Server.MapPath("~/App_Data/" + fileName);
            }

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", fileName);
        }

        private static void ParseProducts(IList<IList<string>> rows, IList<Product> products)
        {
            if (rows == null || rows.Count == 0)
            {
                return;
            }

            var startIndex = HasHeaderRow(rows[0], "ProductID", "ชื่อสินค้า", "ประเภท") ? 1 : 0;
            var headers = startIndex == 1 ? rows[0] : null;
            var idCol = FindColumn(headers, 0, "ProductID", "รหัสสินค้า", "รหัส");
            var nameCol = FindColumn(headers, 1, "ชื่อสินค้า", "Name", "สินค้า");
            var categoryCol = FindColumn(headers, 2, "ประเภท", "Category");
            var priceCol = FindColumn(headers, 3, "ราคาขาย", "ราคา", "SalePrice", "Price");
            var costCol = FindColumn(headers, 4, "ต้นทุน", "Cost");
            var stockCol = FindColumn(headers, 5, "จำนวนคงเหลือ", "คงเหลือ", "Stock");

            for (var index = startIndex; index < rows.Count; index++)
            {
                var row = rows[index];
                var productId = GetCell(row, idCol);
                if (string.IsNullOrWhiteSpace(productId))
                {
                    continue;
                }

                products.Add(new Product
                {
                    ProductId = productId,
                    Name = GetCell(row, nameCol),
                    Category = GetCell(row, categoryCol),
                    SalePrice = GetDecimal(row, priceCol),
                    Cost = GetDecimal(row, costCol),
                    Stock = GetInt(row, stockCol)
                });
            }
        }

        private static void ParseCustomers(IList<IList<string>> rows, IList<Customer> customers)
        {
            if (rows == null || rows.Count == 0)
            {
                return;
            }

            var startIndex = HasHeaderRow(rows[0], "CustomerID", "ชื่อ", "นามสกุล") ? 1 : 0;
            var headers = startIndex == 1 ? rows[0] : null;
            var idCol = FindColumn(headers, 0, "CustomerID", "รหัสลูกค้า", "รหัส");
            var firstCol = FindColumn(headers, 1, "ชื่อ", "FirstName");
            var lastCol = FindColumn(headers, 2, "นามสกุล", "LastName");
            var phoneCol = FindColumn(headers, 3, "เบอร์โทร", "Phone", "โทร");
            var memberCol = FindColumn(headers, 4, "สมาชิก", "Member", "IsMember");

            for (var index = startIndex; index < rows.Count; index++)
            {
                var row = rows[index];
                var customerId = GetCell(row, idCol);
                if (string.IsNullOrWhiteSpace(customerId))
                {
                    continue;
                }

                customers.Add(new Customer
                {
                    CustomerId = customerId,
                    FirstName = GetCell(row, firstCol),
                    LastName = GetCell(row, lastCol),
                    Phone = GetCell(row, phoneCol),
                    IsMember = ParseYesNo(GetCell(row, memberCol))
                });
            }
        }

        private static void ParseEmployees(IList<IList<string>> rows, IList<Employee> employees)
        {
            if (rows == null || rows.Count == 0)
            {
                return;
            }

            var startIndex = HasHeaderRow(rows[0], "EmployeeID", "ชื่อ", "ตำแหน่ง") ? 1 : 0;
            var headers = startIndex == 1 ? rows[0] : null;
            var idCol = FindColumn(headers, 0, "EmployeeID", "รหัสพนักงาน", "รหัส");
            var nameCol = FindColumn(headers, 1, "ชื่อ", "Name");
            var positionCol = FindColumn(headers, 2, "ตำแหน่ง", "Position");
            var salaryCol = FindColumn(headers, 3, "เงินเดือน", "Salary");

            for (var index = startIndex; index < rows.Count; index++)
            {
                var row = rows[index];
                var employeeId = GetCell(row, idCol);
                if (string.IsNullOrWhiteSpace(employeeId))
                {
                    continue;
                }

                employees.Add(new Employee
                {
                    EmployeeId = employeeId,
                    Name = GetCell(row, nameCol),
                    Position = GetCell(row, positionCol),
                    Salary = GetDecimal(row, salaryCol)
                });
            }
        }

        private static void ParseSales(IList<IList<string>> rows, IList<Sale> sales)
        {
            if (rows == null || rows.Count == 0)
            {
                return;
            }

            var startIndex = HasHeaderRow(rows[0], "SaleID", "วันที่", "CustomerID") ? 1 : 0;
            var headers = startIndex == 1 ? rows[0] : null;
            var idCol = FindColumn(headers, 0, "SaleID", "รหัสขาย", "รหัส");
            var dateCol = FindColumn(headers, 1, "วันที่", "Date", "SaleDate");
            var customerCol = FindColumn(headers, 2, "CustomerID", "ลูกค้า");
            var employeeCol = FindColumn(headers, 3, "EmployeeID", "พนักงาน");
            var productCol = FindColumn(headers, 4, "ProductID", "สินค้า");
            var qtyCol = FindColumn(headers, 5, "จำนวน", "Quantity", "Qty");
            var priceCol = FindColumn(headers, 6, "ราคา", "UnitPrice", "Price");
            var totalCol = FindColumn(headers, 7, "รวมเงิน", "Total", "TotalAmount");

            for (var index = startIndex; index < rows.Count; index++)
            {
                var row = rows[index];
                var saleId = GetCell(row, idCol);
                if (string.IsNullOrWhiteSpace(saleId))
                {
                    continue;
                }

                var quantity = GetInt(row, qtyCol);
                var unitPrice = GetDecimal(row, priceCol);
                var total = GetDecimal(row, totalCol);
                if (total == 0m && quantity != 0 && unitPrice != 0m)
                {
                    total = quantity * unitPrice;
                }

                sales.Add(new Sale
                {
                    SaleId = saleId,
                    SaleDate = GetDate(row, dateCol),
                    CustomerId = GetCell(row, customerCol),
                    EmployeeId = GetCell(row, employeeCol),
                    ProductId = GetCell(row, productCol),
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    TotalAmount = total
                });
            }
        }

        private static void ParseCombinedSheet(IList<IList<string>> rows, DashboardData data)
        {
            foreach (var row in rows)
            {
                var columnA = GetCell(row, 0);
                var columnH = GetCell(row, 7);
                var columnI = GetCell(row, 8);

                if (!string.IsNullOrEmpty(columnA) && columnA.StartsWith("P", StringComparison.OrdinalIgnoreCase)
                    && !columnA.StartsWith("Product", StringComparison.OrdinalIgnoreCase))
                {
                    data.Products.Add(new Product
                    {
                        ProductId = columnA,
                        Name = GetCell(row, 1),
                        Category = GetCell(row, 2),
                        SalePrice = GetDecimal(row, 3),
                        Cost = GetDecimal(row, 4),
                        Stock = GetInt(row, 5)
                    });
                }

                if (!string.IsNullOrEmpty(columnI) && columnI.StartsWith("C", StringComparison.OrdinalIgnoreCase)
                    && !columnI.StartsWith("Customer", StringComparison.OrdinalIgnoreCase))
                {
                    data.Customers.Add(new Customer
                    {
                        CustomerId = columnI,
                        FirstName = GetCell(row, 9),
                        LastName = GetCell(row, 10),
                        Phone = GetCell(row, 11),
                        IsMember = ParseYesNo(GetCell(row, 12))
                    });
                }

                if (!string.IsNullOrEmpty(columnH) && columnH.StartsWith("E", StringComparison.OrdinalIgnoreCase)
                    && !columnH.StartsWith("Employee", StringComparison.OrdinalIgnoreCase))
                {
                    data.Employees.Add(new Employee
                    {
                        EmployeeId = columnH,
                        Name = GetCell(row, 8),
                        Position = GetCell(row, 9),
                        Salary = GetDecimal(row, 10)
                    });
                }

                if (!string.IsNullOrEmpty(columnA) && columnA.StartsWith("S", StringComparison.OrdinalIgnoreCase)
                    && !columnA.StartsWith("Sale", StringComparison.OrdinalIgnoreCase))
                {
                    var quantity = GetInt(row, 5);
                    var unitPrice = GetDecimal(row, 6);
                    var total = GetDecimal(row, 7);
                    if (total == 0m)
                    {
                        total = quantity * unitPrice;
                    }

                    data.Sales.Add(new Sale
                    {
                        SaleId = columnA,
                        SaleDate = GetDate(row, 1),
                        CustomerId = GetCell(row, 2),
                        EmployeeId = GetCell(row, 3),
                        ProductId = GetCell(row, 4),
                        Quantity = quantity,
                        UnitPrice = unitPrice,
                        TotalAmount = total
                    });
                }
            }
        }

        private static bool HasHeaderRow(IList<string> row, params string[] expected)
        {
            if (row == null)
            {
                return false;
            }

            var joined = string.Join("|", row.Select(value => (value ?? string.Empty).Trim()));
            return expected.Any(name =>
                joined.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static int FindColumn(IList<string> headers, int fallback, params string[] names)
        {
            if (headers == null)
            {
                return fallback;
            }

            for (var index = 0; index < headers.Count; index++)
            {
                var header = (headers[index] ?? string.Empty).Trim();
                foreach (var name in names)
                {
                    if (string.Equals(header, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return index;
                    }
                }
            }

            return fallback;
        }

        private static string GetCell(IList<string> row, int columnIndex)
        {
            if (row == null || columnIndex < 0 || columnIndex >= row.Count)
            {
                return string.Empty;
            }

            return (row[columnIndex] ?? string.Empty).Trim();
        }

        private static int GetInt(IList<string> row, int columnIndex)
        {
            var text = GetCell(row, columnIndex);
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            decimal parsed;
            if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed))
            {
                return (int)parsed;
            }

            if (decimal.TryParse(text, NumberStyles.Number, new CultureInfo("th-TH"), out parsed))
            {
                return (int)parsed;
            }

            return 0;
        }

        private static decimal GetDecimal(IList<string> row, int columnIndex)
        {
            var text = GetCell(row, columnIndex);
            if (string.IsNullOrEmpty(text))
            {
                return 0m;
            }

            decimal parsed;
            if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }

            if (decimal.TryParse(text, NumberStyles.Number, new CultureInfo("th-TH"), out parsed))
            {
                return parsed;
            }

            return 0m;
        }

        private static DateTime GetDate(IList<string> row, int columnIndex)
        {
            var text = GetCell(row, columnIndex);
            if (string.IsNullOrEmpty(text))
            {
                return DateTime.MinValue;
            }

            DateTime parsed;
            if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
            {
                return parsed;
            }

            if (DateTime.TryParse(text, new CultureInfo("th-TH"), DateTimeStyles.None, out parsed))
            {
                return parsed;
            }

            double oaDate;
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out oaDate)
                && oaDate > 20000
                && oaDate < 80000)
            {
                return DateTime.FromOADate(oaDate);
            }

            return DateTime.MinValue;
        }

        private static bool ParseYesNo(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value.Trim();
            return string.Equals(normalized, "Yes", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Y", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "TRUE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "สมาชิก", StringComparison.OrdinalIgnoreCase);
        }
    }
}
