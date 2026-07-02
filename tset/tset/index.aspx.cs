using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web.UI;
using tset.Models;
using tset.Services;

namespace tset
{
    public partial class index : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                LoadDashboard();
            }
        }

        private void LoadDashboard()
        {
            try
            {
                var service = new ExcelDataService();
                var data = service.LoadDashboardData();

                litLastUpdate.Text = DateTime.Now.ToString("dd MMMM yyyy HH:mm", new CultureInfo("th-TH"));
                litTotalRevenue.Text = data.TotalRevenue.ToString("N0", CultureInfo.InvariantCulture) + " บาท";
                litTotalSales.Text = data.TotalSales.ToString(CultureInfo.InvariantCulture);
                litTotalProducts.Text = data.TotalProducts.ToString(CultureInfo.InvariantCulture);
                litLowStock.Text = data.LowStockCount.ToString(CultureInfo.InvariantCulture);
                litTotalCustomers.Text = data.TotalCustomers.ToString(CultureInfo.InvariantCulture);
                litMemberCount.Text = data.MemberCount.ToString(CultureInfo.InvariantCulture);
                litTotalEmployees.Text = data.TotalEmployees.ToString(CultureInfo.InvariantCulture);
                litTotalProfit.Text = data.TotalProfit.ToString("N0", CultureInfo.InvariantCulture) + " บาท";

                gvSales.DataSource = data.Sales;
                gvSales.DataBind();

                gvProducts.DataSource = data.Products;
                gvProducts.DataBind();

                gvCustomers.DataSource = data.Customers;
                gvCustomers.DataBind();

                gvEmployees.DataSource = data.Employees;
                gvEmployees.DataBind();

                BindChartData(data);
            }
            catch (Exception ex)
            {
                pnlError.Visible = true;
                litError.Text = "<p>" + Server.HtmlEncode(ex.Message) + "</p>";

                if (ex.Message.Contains("ACE.OLEDB") || ex.Message.Contains("provider is not registered"))
                {
                    litError.Text += "<ul>" +
                        "<li>ติดตั้ง <strong>Microsoft Access Database Engine 2016 Redistributable</strong> (ACE OLEDB 12.0)</li>" +
                        "<li>ดาวน์โหลด: <a href='https://www.microsoft.com/en-us/download/details.aspx?id=54920' target='_blank' style='color:#fca5a5;'>Microsoft Download Center</a></li>" +
                        "<li>เลือกเวอร์ชัน x64 หรือ x86 ให้ตรงกับ Visual Studio / IIS Express</li>" +
                        "</ul>";
                }
            }
        }

        private void BindChartData(DashboardSummary data)
        {
            var monthlySales = data.Sales
                .GroupBy(s => s.SaleDate.ToString("MMM yyyy", new CultureInfo("th-TH")))
                .OrderBy(g => g.First().SaleDate)
                .Select(g => new { Label = g.Key, Total = g.Sum(s => s.TotalAmount) })
                .ToList();

            hfMonthlyLabels.Value = string.Join("|", monthlySales.Select(m => m.Label));
            hfMonthlyValues.Value = string.Join("|", monthlySales.Select(m => m.Total.ToString(CultureInfo.InvariantCulture)));

            var categorySales = new Dictionary<string, decimal>();
            foreach (var sale in data.Sales)
            {
                var product = data.Products.FirstOrDefault(p => p.ProductId == sale.ProductId);
                var category = product != null ? product.Category : "อื่นๆ";
                if (!categorySales.ContainsKey(category))
                {
                    categorySales[category] = 0;
                }
                categorySales[category] += sale.TotalAmount;
            }

            hfCategoryLabels.Value = string.Join("|", categorySales.Keys);
            hfCategoryValues.Value = string.Join("|", categorySales.Values.Select(v => v.ToString(CultureInfo.InvariantCulture)));
        }
    }
}
