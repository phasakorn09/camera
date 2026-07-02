using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.OleDb;
using System.Globalization;
using System.IO;
using System.Web;
using tset.Models;

namespace tset.Services
{
    /// <summary>
    /// อ่านข้อมูลจากไฟล์ Excel (.xlsx) ด้วย Microsoft ACE OLEDB 12.0
    /// โครงสร้างไฟล์ test4.xlsx มีหลายตารางใน Sheet1:
    ///   - สินค้า (A2:F12), ลูกค้า (I2:M7), พนักงาน (H10:L14), การขาย (A17:H27)
    /// </summary>
    public class ExcelDataService
    {
        private const string AceProvider = "Microsoft.ACE.OLEDB.12.0";
        private readonly string _excelPath;

        public ExcelDataService()
        {
            var relativePath = ConfigurationManager.AppSettings["ExcelFilePath"] ?? "~/App_Data/test4.xlsx";
            _excelPath = HttpContext.Current.Server.MapPath(relativePath);
        }

        public ExcelDataService(string absolutePath)
        {
            _excelPath = absolutePath;
        }

        public DashboardSummary LoadDashboardData()
        {
            if (!File.Exists(_excelPath))
            {
                throw new FileNotFoundException("ไม่พบไฟล์ Excel: " + _excelPath);
            }

            var summary = new DashboardSummary
            {
                Products = LoadProducts(),
                Customers = LoadCustomers(),
                Employees = LoadEmployees(),
                Sales = LoadSales()
            };

            summary.TotalProducts = summary.Products.Count;
            summary.TotalCustomers = summary.Customers.Count;
            summary.TotalEmployees = summary.Employees.Count;
            summary.TotalSales = summary.Sales.Count;

            foreach (var sale in summary.Sales)
            {
                summary.TotalRevenue += sale.TotalAmount;
            }

            foreach (var product in summary.Products)
            {
                if (product.Stock < 100)
                {
                    summary.LowStockCount++;
                }
            }

            foreach (var customer in summary.Customers)
            {
                if (string.Equals(customer.IsMember, "Yes", StringComparison.OrdinalIgnoreCase))
                {
                    summary.MemberCount++;
                }
            }

            foreach (var sale in summary.Sales)
            {
                var product = summary.Products.Find(p => p.ProductId == sale.ProductId);
                if (product != null)
                {
                    summary.TotalProfit += (product.SalePrice - product.Cost) * sale.Quantity;
                }
            }

            return summary;
        }

        public List<Product> LoadProducts()
        {
            return ReadRangeAsList(
                "SELECT * FROM [Sheet1$A2:F12]",
                row => new Product
                {
                    ProductId = GetString(row, "ProductID"),
                    Name = GetString(row, "ชื่อสินค้า"),
                    Category = GetString(row, "ประเภท"),
                    SalePrice = GetDecimal(row, "ราคาขาย"),
                    Cost = GetDecimal(row, "ต้นทุน"),
                    Stock = GetInt(row, "จำนวนคงเหลือ")
                },
                item => !string.IsNullOrWhiteSpace(item.ProductId));
        }

        public List<Customer> LoadCustomers()
        {
            return ReadRangeAsList(
                "SELECT * FROM [Sheet1$I2:M7]",
                row => new Customer
                {
                    CustomerId = GetString(row, "CustomerID"),
                    FirstName = GetString(row, "ชื่อ"),
                    LastName = GetString(row, "นามสกุล"),
                    Phone = GetString(row, "เบอร์โทร"),
                    IsMember = GetString(row, "สมาชิก")
                },
                item => !string.IsNullOrWhiteSpace(item.CustomerId));
        }

        public List<Employee> LoadEmployees()
        {
            return ReadRangeAsList(
                "SELECT * FROM [Sheet1$H10:L14]",
                row => new Employee
                {
                    EmployeeId = GetString(row, "EmployeeID"),
                    Name = GetString(row, "ชื่อ"),
                    Position = GetString(row, "ตำแหน่ง"),
                    Salary = GetDecimal(row, "เงินเดือน")
                },
                item => !string.IsNullOrWhiteSpace(item.EmployeeId));
        }

        public List<Sale> LoadSales()
        {
            return ReadRangeAsList(
                "SELECT * FROM [Sheet1$A17:H27]",
                row => new Sale
                {
                    SaleId = GetString(row, "SaleID"),
                    SaleDate = GetDate(row, "วันที่"),
                    CustomerId = GetString(row, "CustomerID"),
                    EmployeeId = GetString(row, "EmployeeID"),
                    ProductId = GetString(row, "ProductID"),
                    Quantity = GetInt(row, "จำนวน"),
                    UnitPrice = GetDecimal(row, "ราคาต่อหน่วย"),
                    TotalAmount = GetDecimal(row, "รวมเงิน")
                },
                item => !string.IsNullOrWhiteSpace(item.SaleId));
        }

        private string ConnectionString
        {
            get
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "Provider={0};Data Source={1};Extended Properties='Excel 12.0 Xml;HDR=YES;IMEX=1';",
                    AceProvider,
                    _excelPath);
            }
        }

        private List<T> ReadRangeAsList<T>(string query, Func<DataRow, T> map, Func<T, bool> filter)
        {
            var results = new List<T>();
            var table = ExecuteQuery(query);

            foreach (DataRow row in table.Rows)
            {
                var item = map(row);
                if (filter(item))
                {
                    results.Add(item);
                }
            }

            return results;
        }

        private DataTable ExecuteQuery(string query)
        {
            var table = new DataTable();

            using (var connection = new OleDbConnection(ConnectionString))
            {
                connection.Open();
                using (var adapter = new OleDbDataAdapter(query, connection))
                {
                    adapter.Fill(table);
                }
            }

            return table;
        }

        private static string GetString(DataRow row, string columnName)
        {
            if (!row.Table.Columns.Contains(columnName))
            {
                return string.Empty;
            }

            var value = row[columnName];
            return value == DBNull.Value ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture).Trim();
        }

        private static int GetInt(DataRow row, string columnName)
        {
            if (!row.Table.Columns.Contains(columnName))
            {
                return 0;
            }

            var value = row[columnName];
            if (value == DBNull.Value)
            {
                return 0;
            }

            int result;
            return int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out result)
                ? result
                : 0;
        }

        private static decimal GetDecimal(DataRow row, string columnName)
        {
            if (!row.Table.Columns.Contains(columnName))
            {
                return 0m;
            }

            var value = row[columnName];
            if (value == DBNull.Value)
            {
                return 0m;
            }

            decimal result;
            return decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Number, CultureInfo.InvariantCulture, out result)
                ? result
                : 0m;
        }

        private static DateTime GetDate(DataRow row, string columnName)
        {
            if (!row.Table.Columns.Contains(columnName))
            {
                return DateTime.MinValue;
            }

            var value = row[columnName];
            if (value == DBNull.Value)
            {
                return DateTime.MinValue;
            }

            if (value is DateTime)
            {
                return (DateTime)value;
            }

            DateTime result;
            return DateTime.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, DateTimeStyles.None, out result)
                ? result
                : DateTime.MinValue;
        }
    }
}
