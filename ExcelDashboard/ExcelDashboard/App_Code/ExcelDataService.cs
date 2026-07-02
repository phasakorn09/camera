using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.OleDb;
using System.Globalization;
using System.IO;
using System.Web;

namespace ExcelDashboard
{
    public class ExcelDataService
    {
        private readonly string _connectionString;

        public ExcelDataService()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["ExcelFile"].ConnectionString;
        }

        public string ExcelFilePath
        {
            get
            {
                var dataDirectory = HttpContext.Current != null
                    ? HttpContext.Current.Server.MapPath("~/App_Data/test4.xlsx")
                    : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "test4.xlsx");
                return dataDirectory;
            }
        }

        public bool ExcelFileExists()
        {
            return File.Exists(ExcelFilePath);
        }

        public List<Product> GetProducts()
        {
            const string query = "SELECT * FROM [Sheet1$A2$F12]";
            var table = ExecuteQuery(query);
            var products = new List<Product>();

            foreach (DataRow row in table.Rows)
            {
                var productId = GetString(row, "ProductID");
                if (string.IsNullOrWhiteSpace(productId))
                {
                    continue;
                }

                products.Add(new Product
                {
                    ProductId = productId,
                    Name = GetString(row, "ชื่อสินค้า"),
                    Category = GetString(row, "ประเภท"),
                    SalePrice = GetDecimal(row, "ราคาขาย"),
                    Cost = GetDecimal(row, "ต้นทุน"),
                    Stock = GetInt(row, "จำนวนคงเหลือ")
                });
            }

            return products;
        }

        public List<Customer> GetCustomers()
        {
            const string query = "SELECT * FROM [Sheet1$I2$M7]";
            var table = ExecuteQuery(query);
            var customers = new List<Customer>();

            foreach (DataRow row in table.Rows)
            {
                var customerId = GetString(row, "CustomerID");
                if (string.IsNullOrWhiteSpace(customerId))
                {
                    continue;
                }

                customers.Add(new Customer
                {
                    CustomerId = customerId,
                    FirstName = GetString(row, "ชื่อ"),
                    LastName = GetString(row, "นามสกุล"),
                    Phone = GetString(row, "เบอร์โทร"),
                    Member = GetString(row, "สมาชิก")
                });
            }

            return customers;
        }

        public List<Employee> GetEmployees()
        {
            const string query = "SELECT * FROM [Sheet1$H10$K14]";
            var table = ExecuteQuery(query);
            var employees = new List<Employee>();

            foreach (DataRow row in table.Rows)
            {
                var employeeId = GetString(row, "EmployeeID");
                if (string.IsNullOrWhiteSpace(employeeId))
                {
                    continue;
                }

                employees.Add(new Employee
                {
                    EmployeeId = employeeId,
                    Name = GetString(row, "ชื่อ"),
                    Position = GetString(row, "ตำแหน่ง"),
                    Salary = GetDecimal(row, "เงินเดือน")
                });
            }

            return employees;
        }

        public List<Sale> GetSales()
        {
            const string query = "SELECT * FROM [Sheet1$A17$H27]";
            var table = ExecuteQuery(query);
            var sales = new List<Sale>();

            foreach (DataRow row in table.Rows)
            {
                var saleId = GetString(row, "SaleID");
                if (string.IsNullOrWhiteSpace(saleId))
                {
                    continue;
                }

                sales.Add(new Sale
                {
                    SaleId = saleId,
                    SaleDate = GetExcelDate(row, "วันที่"),
                    CustomerId = GetString(row, "CustomerID"),
                    EmployeeId = GetString(row, "EmployeeID"),
                    ProductId = GetString(row, "ProductID"),
                    Quantity = GetInt(row, "จำนวน"),
                    UnitPrice = GetDecimal(row, "ราคาต่อหน่วย"),
                    Total = GetDecimal(row, "รวมเงิน")
                });
            }

            return sales;
        }

        public DashboardSummary GetSummary(List<Product> products, List<Customer> customers, List<Employee> employees, List<Sale> sales)
        {
            var summary = new DashboardSummary
            {
                ProductCount = products.Count,
                CustomerCount = customers.Count,
                EmployeeCount = employees.Count,
                SaleCount = sales.Count,
                TotalRevenue = 0,
                TotalProfit = 0,
                LowStockCount = 0
            };

            var productLookup = new Dictionary<string, Product>();
            foreach (var product in products)
            {
                productLookup[product.ProductId] = product;
                if (product.Stock < 100)
                {
                    summary.LowStockCount++;
                }
            }

            foreach (var sale in sales)
            {
                summary.TotalRevenue += sale.Total;

                if (productLookup.ContainsKey(sale.ProductId))
                {
                    var product = productLookup[sale.ProductId];
                    summary.TotalProfit += (product.SalePrice - product.Cost) * sale.Quantity;
                }
            }

            return summary;
        }

        private DataTable ExecuteQuery(string query)
        {
            if (!ExcelFileExists())
            {
                throw new FileNotFoundException("ไม่พบไฟล์ Excel ที่ App_Data/test4.xlsx", ExcelFilePath);
            }

            var resolvedConnectionString = _connectionString.Replace("|DataDirectory|", Path.GetDirectoryName(ExcelFilePath));

            using (var connection = new OleDbConnection(resolvedConnectionString))
            using (var command = new OleDbCommand(query, connection))
            using (var adapter = new OleDbDataAdapter(command))
            {
                var table = new DataTable();
                connection.Open();
                adapter.Fill(table);
                return table;
            }
        }

        private static string GetString(DataRow row, params string[] columnNames)
        {
            foreach (var columnName in columnNames)
            {
                if (row.Table.Columns.Contains(columnName) && row[columnName] != DBNull.Value)
                {
                    return Convert.ToString(row[columnName]).Trim();
                }
            }

            return string.Empty;
        }

        private static int GetInt(DataRow row, params string[] columnNames)
        {
            var value = GetString(row, columnNames);
            int result;
            return int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result) ? result : 0;
        }

        private static decimal GetDecimal(DataRow row, params string[] columnNames)
        {
            var value = GetString(row, columnNames);
            decimal result;
            return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result) ? result : 0m;
        }

        private static DateTime GetExcelDate(DataRow row, params string[] columnNames)
        {
            foreach (var columnName in columnNames)
            {
                if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value)
                {
                    continue;
                }

                var rawValue = row[columnName];

                if (rawValue is DateTime)
                {
                    return (DateTime)rawValue;
                }

                double serial;
                if (double.TryParse(Convert.ToString(rawValue), NumberStyles.Any, CultureInfo.InvariantCulture, out serial))
                {
                    return DateTime.FromOADate(serial);
                }

                DateTime parsed;
                if (DateTime.TryParse(Convert.ToString(rawValue), CultureInfo.GetCultureInfo("th-TH"), DateTimeStyles.None, out parsed))
                {
                    return parsed;
                }
            }

            return DateTime.MinValue;
        }
    }
}
