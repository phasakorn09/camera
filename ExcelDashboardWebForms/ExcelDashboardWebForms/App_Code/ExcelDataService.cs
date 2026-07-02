using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.OleDb;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web;

namespace ExcelDashboardWebForms
{
    public class ExcelDataService
    {
        private const string AceProvider = "Microsoft.ACE.OLEDB.12.0";
        private const string SheetName = "Sheet1$";

        public DashboardData LoadDashboard()
        {
            var excelPath = ResolveExcelPath();
            var rows = ReadAllRows(excelPath);

            var products = ParseProducts(rows);
            var customers = ParseCustomers(rows);
            var employees = ParseEmployees(rows);
            var sales = ParseSales(rows);

            var productLookup = products.ToDictionary(p => p.ProductId, p => p);
            var customerLookup = customers.ToDictionary(c => c.CustomerId, c => c);

            return new DashboardData
            {
                ExcelPath = excelPath,
                LoadedAt = DateTime.Now,
                Products = products,
                Customers = customers,
                Employees = employees,
                Sales = sales,
                CategorySummaries = BuildCategorySummaries(products),
                SalesByProduct = BuildSalesByProduct(sales, productLookup),
                SalesByDate = BuildSalesByDate(sales),
                Summary = BuildSummary(products, customers, employees, sales, productLookup, customerLookup)
            };
        }

        private static string ResolveExcelPath()
        {
            var fileName = ConfigurationManager.AppSettings["ExcelFileName"] ?? "test4.xlsx";
            var path = HttpContext.Current.Server.MapPath("~/App_Data/" + fileName);

            if (!File.Exists(path))
            {
                throw new FileNotFoundException("ไม่พบไฟล์ Excel ที่ App_Data/" + fileName, path);
            }

            return path;
        }

        private static string BuildConnectionString(string excelPath)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "Provider={0};Data Source={1};Extended Properties=\"Excel 12.0 Xml;HDR=NO;IMEX=1\";",
                AceProvider,
                excelPath);
        }

        private List<Dictionary<string, string>> ReadAllRows(string excelPath)
        {
            var result = new List<Dictionary<string, string>>();

            using (var connection = new OleDbConnection(BuildConnectionString(excelPath)))
            {
                connection.Open();
                var query = "SELECT * FROM [" + SheetName + "]";
                using (var command = new OleDbCommand(query, connection))
                using (var reader = command.ExecuteReader())
                {
                    var fieldCount = reader.FieldCount;
                    var columnNames = new string[fieldCount];
                    for (var i = 0; i < fieldCount; i++)
                    {
                        columnNames[i] = reader.GetName(i);
                    }

                    while (reader.Read())
                    {
                        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        for (var i = 0; i < fieldCount; i++)
                        {
                            row[columnNames[i]] = reader.IsDBNull(i) ? string.Empty : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture).Trim();
                        }
                        result.Add(row);
                    }
                }
            }

            return result;
        }

        private static string GetCell(Dictionary<string, string> row, string column)
        {
            string value;
            return row.TryGetValue(column, out value) ? value.Trim() : string.Empty;
        }

        private static List<Product> ParseProducts(List<Dictionary<string, string>> rows)
        {
            var products = new List<Product>();

            for (var i = 2; i < rows.Count; i++)
            {
                var row = rows[i];
                var productId = GetCell(row, "F1");
                if (!productId.StartsWith("P", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                products.Add(new Product
                {
                    ProductId = productId,
                    Name = GetCell(row, "F2"),
                    Category = GetCell(row, "F3"),
                    SalePrice = ParseDecimal(GetCell(row, "F4")),
                    Cost = ParseDecimal(GetCell(row, "F5")),
                    Stock = ParseInt(GetCell(row, "F6"))
                });
            }

            return products;
        }

        private static List<Customer> ParseCustomers(List<Dictionary<string, string>> rows)
        {
            var customers = new List<Customer>();

            for (var i = 2; i < rows.Count; i++)
            {
                var row = rows[i];
                var customerId = GetCell(row, "F9");
                if (!customerId.StartsWith("C", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                customers.Add(new Customer
                {
                    CustomerId = customerId,
                    FirstName = GetCell(row, "F10"),
                    LastName = GetCell(row, "F11"),
                    Phone = GetCell(row, "F12"),
                    IsMember = IsYes(GetCell(row, "F13"))
                });
            }

            return customers;
        }

        private static List<Employee> ParseEmployees(List<Dictionary<string, string>> rows)
        {
            var employees = new List<Employee>();

            for (var i = 10; i < rows.Count; i++)
            {
                var row = rows[i];
                var employeeId = GetCell(row, "F8");
                if (!employeeId.StartsWith("E", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                employees.Add(new Employee
                {
                    EmployeeId = employeeId,
                    Name = GetCell(row, "F9"),
                    Position = GetCell(row, "F10"),
                    Salary = ParseDecimal(GetCell(row, "F11"))
                });
            }

            return employees;
        }

        private static List<Sale> ParseSales(List<Dictionary<string, string>> rows)
        {
            var sales = new List<Sale>();

            for (var i = 17; i < rows.Count; i++)
            {
                var row = rows[i];
                var saleId = GetCell(row, "F1");
                if (!saleId.StartsWith("S", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                sales.Add(new Sale
                {
                    SaleId = saleId,
                    SaleDate = ParseExcelDate(GetCell(row, "F2")),
                    CustomerId = GetCell(row, "F3"),
                    EmployeeId = GetCell(row, "F4"),
                    ProductId = GetCell(row, "F5"),
                    Quantity = ParseInt(GetCell(row, "F6")),
                    UnitPrice = ParseDecimal(GetCell(row, "F7")),
                    TotalAmount = ParseDecimal(GetCell(row, "F8"))
                });
            }

            return sales;
        }

        private static List<CategorySummary> BuildCategorySummaries(List<Product> products)
        {
            return products
                .GroupBy(p => p.Category)
                .Select(g => new CategorySummary
                {
                    Category = g.Key,
                    ProductCount = g.Count(),
                    TotalStock = g.Sum(p => p.Stock),
                    TotalValue = g.Sum(p => p.SalePrice * p.Stock)
                })
                .OrderByDescending(c => c.TotalValue)
                .ToList();
        }

        private static List<SalesByProductSummary> BuildSalesByProduct(List<Sale> sales, Dictionary<string, Product> productLookup)
        {
            return sales
                .GroupBy(s => s.ProductId)
                .Select(g =>
                {
                    Product product;
                    var name = productLookup.TryGetValue(g.Key, out product) ? product.Name : g.Key;
                    return new SalesByProductSummary
                    {
                        ProductName = name,
                        QuantitySold = g.Sum(s => s.Quantity),
                        Revenue = g.Sum(s => s.TotalAmount)
                    };
                })
                .OrderByDescending(s => s.Revenue)
                .ToList();
        }

        private static List<SalesByDateSummary> BuildSalesByDate(List<Sale> sales)
        {
            return sales
                .GroupBy(s => s.SaleDate.Date)
                .OrderBy(g => g.Key)
                .Select(g => new SalesByDateSummary
                {
                    DateLabel = g.Key.ToString("dd MMM yyyy", new CultureInfo("th-TH")),
                    Revenue = g.Sum(s => s.TotalAmount),
                    OrderCount = g.Count()
                })
                .ToList();
        }

        private static DashboardSummary BuildSummary(
            List<Product> products,
            List<Customer> customers,
            List<Employee> employees,
            List<Sale> sales,
            Dictionary<string, Product> productLookup,
            Dictionary<string, Customer> customerLookup)
        {
            var totalRevenue = sales.Sum(s => s.TotalAmount);
            var totalCost = sales.Sum(s =>
            {
                Product product;
                if (productLookup.TryGetValue(s.ProductId, out product))
                {
                    return product.Cost * s.Quantity;
                }
                return 0m;
            });

            var topProductGroup = sales
                .GroupBy(s => s.ProductId)
                .OrderByDescending(g => g.Sum(x => x.TotalAmount))
                .FirstOrDefault();

            var topCustomerGroup = sales
                .GroupBy(s => s.CustomerId)
                .OrderByDescending(g => g.Sum(x => x.TotalAmount))
                .FirstOrDefault();

            string topProductName = "-";
            if (topProductGroup != null)
            {
                Product topProduct;
                topProductName = productLookup.TryGetValue(topProductGroup.Key, out topProduct)
                    ? topProduct.Name
                    : topProductGroup.Key;
            }

            string topCustomerName = "-";
            if (topCustomerGroup != null)
            {
                Customer topCustomer;
                topCustomerName = customerLookup.TryGetValue(topCustomerGroup.Key, out topCustomer)
                    ? topCustomer.FullName
                    : topCustomerGroup.Key;
            }

            return new DashboardSummary
            {
                ProductCount = products.Count,
                CustomerCount = customers.Count,
                EmployeeCount = employees.Count,
                SaleCount = sales.Count,
                TotalRevenue = totalRevenue,
                TotalCost = totalCost,
                TotalProfit = totalRevenue - totalCost,
                TotalStock = products.Sum(p => p.Stock),
                MemberCount = customers.Count(c => c.IsMember),
                AverageOrderValue = sales.Count == 0 ? 0m : totalRevenue / sales.Count,
                TopProduct = topProductName,
                TopCustomer = topCustomerName
            };
        }

        private static decimal ParseDecimal(string value)
        {
            decimal number;
            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out number))
            {
                return number;
            }

            return decimal.TryParse(value, NumberStyles.Any, CultureInfo.GetCultureInfo("th-TH"), out number) ? number : 0m;
        }

        private static int ParseInt(string value)
        {
            int number;
            return int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out number) ? number : 0;
        }

        private static bool IsYes(string value)
        {
            return string.Equals(value, "Yes", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Y", StringComparison.OrdinalIgnoreCase)
                || value == "1";
        }

        private static DateTime ParseExcelDate(string value)
        {
            double oaDate;
            if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out oaDate))
            {
                try
                {
                    return DateTime.FromOADate(oaDate);
                }
                catch (ArgumentException)
                {
                }
            }

            DateTime parsed;
            if (DateTime.TryParse(value, CultureInfo.GetCultureInfo("th-TH"), DateTimeStyles.None, out parsed))
            {
                return parsed;
            }

            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed) ? parsed : DateTime.MinValue;
        }
    }
}
