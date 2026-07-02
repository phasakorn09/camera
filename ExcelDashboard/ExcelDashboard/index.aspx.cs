using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Script.Serialization;
using System.Web.UI;

namespace ExcelDashboard
{
    public partial class index : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (IsPostBack)
            {
                return;
            }

            try
            {
                var service = new ExcelDataService();
                var products = service.GetProducts();
                var customers = service.GetCustomers();
                var employees = service.GetEmployees();
                var sales = service.GetSales();
                var summary = service.GetSummary(products, customers, employees, sales);

                BindSummary(summary);
                BindProducts(products);
                BindCustomers(customers);
                BindEmployees(employees);
                BindSales(sales);
                BindChartData(sales, products);

                lblStatus.Text = "Live — " + service.ExcelFilePath;
            }
            catch (Exception ex)
            {
                pnlError.Visible = true;
                lblError.Text = "เกิดข้อผิดพลาดในการอ่านข้อมูล Excel: " + ex.Message
                    + "<br/><small>ตรวจสอบว่าติดตั้ง Microsoft Access Database Engine (ACE OLEDB 12.0) แล้ว และ Platform target เป็น x86</small>";
            }
        }

        private void BindSummary(DashboardSummary summary)
        {
            var th = CultureInfo.GetCultureInfo("th-TH");
            lblTotalRevenue.Text = summary.TotalRevenue.ToString("N0", th);
            lblTotalProfit.Text = summary.TotalProfit.ToString("N0", th);
            lblProductCount.Text = summary.ProductCount.ToString();
            lblCustomerCount.Text = summary.CustomerCount.ToString();
            lblEmployeeCount.Text = summary.EmployeeCount.ToString();
            lblLowStock.Text = summary.LowStockCount.ToString();
        }

        private void BindProducts(List<Product> products)
        {
            var th = CultureInfo.GetCultureInfo("th-TH");
            gvProducts.DataSource = products.Select(p => new
            {
                p.ProductId,
                p.Name,
                p.Category,
                SalePriceText = p.SalePrice.ToString("N0", th),
                p.Stock
            }).ToList();
            gvProducts.DataBind();
        }

        private void BindCustomers(List<Customer> customers)
        {
            gvCustomers.DataSource = customers;
            gvCustomers.DataBind();
        }

        private void BindEmployees(List<Employee> employees)
        {
            var th = CultureInfo.GetCultureInfo("th-TH");
            gvEmployees.DataSource = employees.Select(e => new
            {
                e.EmployeeId,
                e.Name,
                e.Position,
                SalaryText = e.Salary.ToString("N0", th)
            }).ToList();
            gvEmployees.DataBind();
        }

        private void BindSales(List<Sale> sales)
        {
            var th = CultureInfo.GetCultureInfo("th-TH");
            gvSales.DataSource = sales.Select(s => new
            {
                s.SaleId,
                SaleDateText = s.SaleDate.ToString("dd MMM yyyy", th),
                s.CustomerId,
                s.EmployeeId,
                s.ProductId,
                s.Quantity,
                UnitPriceText = s.UnitPrice.ToString("N0", th),
                TotalText = s.Total.ToString("N0", th)
            }).ToList();
            gvSales.DataBind();
        }

        private void BindChartData(List<Sale> sales, List<Product> products)
        {
            var th = CultureInfo.GetCultureInfo("th-TH");
            var productLookup = products.ToDictionary(p => p.ProductId, p => p);

            var salesByMonth = sales
                .GroupBy(s => new { s.SaleDate.Year, s.SaleDate.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new
                {
                    Label = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy", th),
                    Value = g.Sum(s => s.Total)
                })
                .ToList();

            var salesByCategory = sales
                .GroupBy(s =>
                {
                    Product product;
                    return productLookup.TryGetValue(s.ProductId, out product) ? product.Category : "อื่นๆ";
                })
                .Select(g => new { Label = g.Key, Value = g.Sum(s => s.Total) })
                .OrderByDescending(g => g.Value)
                .ToList();

            var chartData = new
            {
                salesByMonth = new
                {
                    labels = salesByMonth.Select(x => x.Label).ToArray(),
                    values = salesByMonth.Select(x => (double)x.Value).ToArray()
                },
                salesByCategory = new
                {
                    labels = salesByCategory.Select(x => x.Label).ToArray(),
                    values = salesByCategory.Select(x => (double)x.Value).ToArray()
                }
            };

            hfChartData.Value = new JavaScriptSerializer().Serialize(chartData);
        }
    }
}
