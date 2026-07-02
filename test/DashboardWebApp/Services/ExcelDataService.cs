using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.OleDb;
using System.Globalization;
using System.IO;
using System.Web;
using DashboardWebApp.Models;

namespace DashboardWebApp.Services
{
    public class ExcelDataService
    {
        private const string AceProvider = "Microsoft.ACE.OLEDB.12.0";

        public DashboardData LoadDashboardData()
        {
            var excelPath = ResolveExcelPath();
            if (!File.Exists(excelPath))
            {
                throw new FileNotFoundException("ไม่พบไฟล์ Excel ใน App_Data", excelPath);
            }

            var connectionString = string.Format(
                CultureInfo.InvariantCulture,
                "Provider={0};Data Source={1};Extended Properties='Excel 12.0 Xml;HDR=NO;IMEX=1';",
                AceProvider,
                excelPath);

            var dashboardData = new DashboardData
            {
                Products = new List<Product>(),
                Customers = new List<Customer>(),
                Employees = new List<Employee>(),
                Sales = new List<Sale>()
            };

            using (var connection = new OleDbConnection(connectionString))
            {
                connection.Open();
                using (var table = connection.GetOleDbSchemaTable(OleDbSchemaGuid.Tables, null))
                {
                    if (table == null || table.Rows.Count == 0)
                    {
                        throw new InvalidOperationException("ไม่พบ Sheet ในไฟล์ Excel");
                    }
                }

                var rows = ReadAllRows(connection);
                ParseWorksheetRows(rows, dashboardData);
            }

            return dashboardData;
        }

        private static string ResolveExcelPath()
        {
            var fileName = ConfigurationManager.AppSettings["ExcelFileName"] ?? "test4.xlsx";
            return HttpContext.Current.Server.MapPath(string.Format(CultureInfo.InvariantCulture, "~/App_Data/{0}", fileName));
        }

        private static List<Dictionary<int, object>> ReadAllRows(OleDbConnection connection)
        {
            var rows = new List<Dictionary<int, object>>();

            using (var command = new OleDbCommand("SELECT * FROM [Sheet1$]", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var row = new Dictionary<int, object>();
                    for (var columnIndex = 0; columnIndex < reader.FieldCount; columnIndex++)
                    {
                        row[columnIndex] = reader.IsDBNull(columnIndex) ? null : reader.GetValue(columnIndex);
                    }

                    rows.Add(row);
                }
            }

            return rows;
        }

        private static void ParseWorksheetRows(IList<Dictionary<int, object>> rows, DashboardData dashboardData)
        {
            foreach (var row in rows)
            {
                var columnA = GetCellString(row, 0);
                var columnH = GetCellString(row, 7);
                var columnI = GetCellString(row, 8);

                if (!string.IsNullOrEmpty(columnA) && columnA.StartsWith("P", StringComparison.OrdinalIgnoreCase))
                {
                    dashboardData.Products.Add(new Product
                    {
                        ProductId = columnA,
                        Name = GetCellString(row, 1),
                        Category = GetCellString(row, 2),
                        SalePrice = GetCellDecimal(row, 3),
                        Cost = GetCellDecimal(row, 4),
                        Stock = GetCellInt(row, 5)
                    });
                }

                if (!string.IsNullOrEmpty(columnI) && columnI.StartsWith("C", StringComparison.OrdinalIgnoreCase))
                {
                    dashboardData.Customers.Add(new Customer
                    {
                        CustomerId = columnI,
                        FirstName = GetCellString(row, 9),
                        LastName = GetCellString(row, 10),
                        Phone = GetCellString(row, 11),
                        IsMember = string.Equals(GetCellString(row, 12), "Yes", StringComparison.OrdinalIgnoreCase)
                    });
                }

                if (!string.IsNullOrEmpty(columnH) && columnH.StartsWith("E", StringComparison.OrdinalIgnoreCase))
                {
                    dashboardData.Employees.Add(new Employee
                    {
                        EmployeeId = columnH,
                        Name = GetCellString(row, 8),
                        Position = GetCellString(row, 9),
                        Salary = GetCellDecimal(row, 10)
                    });
                }

                if (!string.IsNullOrEmpty(columnA) && columnA.StartsWith("S", StringComparison.OrdinalIgnoreCase))
                {
                    dashboardData.Sales.Add(new Sale
                    {
                        SaleId = columnA,
                        SaleDate = GetCellDate(row, 1),
                        CustomerId = GetCellString(row, 2),
                        EmployeeId = GetCellString(row, 3),
                        ProductId = GetCellString(row, 4),
                        Quantity = GetCellInt(row, 5),
                        UnitPrice = GetCellDecimal(row, 6),
                        TotalAmount = GetCellDecimal(row, 7)
                    });
                }
            }
        }

        private static string GetCellString(Dictionary<int, object> row, int columnIndex)
        {
            object value;
            if (!row.TryGetValue(columnIndex, out value) || value == null)
            {
                return string.Empty;
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture).Trim();
        }

        private static int GetCellInt(Dictionary<int, object> row, int columnIndex)
        {
            object value;
            if (!row.TryGetValue(columnIndex, out value) || value == null)
            {
                return 0;
            }

            if (value is double)
            {
                return Convert.ToInt32((double)value, CultureInfo.InvariantCulture);
            }

            int parsed;
            return int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : 0;
        }

        private static decimal GetCellDecimal(Dictionary<int, object> row, int columnIndex)
        {
            object value;
            if (!row.TryGetValue(columnIndex, out value) || value == null)
            {
                return 0m;
            }

            if (value is double)
            {
                return Convert.ToDecimal((double)value, CultureInfo.InvariantCulture);
            }

            decimal parsed;
            return decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Number, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : 0m;
        }

        private static DateTime GetCellDate(Dictionary<int, object> row, int columnIndex)
        {
            object value;
            if (!row.TryGetValue(columnIndex, out value) || value == null)
            {
                return DateTime.MinValue;
            }

            if (value is double)
            {
                return DateTime.FromOADate((double)value);
            }

            DateTime parsed;
            if (DateTime.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
            {
                return parsed;
            }

            return DateTime.MinValue;
        }
    }
}
