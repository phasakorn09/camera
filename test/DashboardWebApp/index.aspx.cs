using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.UI;
using DashboardWebApp.Services;

namespace DashboardWebApp
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
                var data = service.LoadDashboardData();
                BindDashboard(data);
                pnlDashboard.Visible = true;
                pnlError.Visible = false;
            }
            catch (Exception ex)
            {
                pnlDashboard.Visible = false;
                pnlError.Visible = true;
                lblError.Text = string.Format(
                    CultureInfo.InvariantCulture,
                    "ไม่สามารถโหลดข้อมูลจาก Excel ได้: {0}<br /><br />กรุณาติดตั้ง <strong>Microsoft Access Database Engine 2016 Redistributable (ACE OLEDB 12.0)</strong> และตรวจสอบว่าไฟล์ test4.xlsx อยู่ใน App_Data",
                    Server.HtmlEncode(ex.Message));
            }
        }

        private void BindDashboard(Models.DashboardData data)
        {
            lblTotalRevenue.Text = FormatCurrency(data.TotalRevenue);
            lblTotalProducts.Text = data.TotalProducts.ToString(CultureInfo.InvariantCulture);
            lblTotalCustomers.Text = string.Format(
                CultureInfo.InvariantCulture,
                "{0} / {1}",
                data.TotalCustomers,
                data.MemberCount);
            lblTotalEmployees.Text = data.TotalEmployees.ToString(CultureInfo.InvariantCulture);
            lblTotalSales.Text = data.TotalSalesRecords.ToString(CultureInfo.InvariantCulture);
            lblStockValue.Text = FormatCurrency(data.TotalStockValue);

            gvSales.DataSource = data.Sales
                .OrderByDescending(s => s.SaleDate)
                .Select(s => new
                {
                    s.SaleId,
                    SaleDateDisplay = s.SaleDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                    s.ProductId,
                    s.Quantity,
                    TotalAmountDisplay = FormatCurrency(s.TotalAmount)
                })
                .ToList();
            gvSales.DataBind();

            gvProducts.DataSource = data.Products.Select(p => new
            {
                p.ProductId,
                p.Name,
                p.Category,
                SalePriceDisplay = FormatCurrency(p.SalePrice),
                StockDisplay = p.Stock.ToString(CultureInfo.InvariantCulture)
            }).ToList();
            gvProducts.DataBind();

            gvCustomers.DataSource = data.Customers.Select(c => new
            {
                c.CustomerId,
                c.FullName,
                c.Phone,
                MemberBadge = c.IsMember
                    ? "<span class='badge badge-success'>สมาชิก</span>"
                    : "<span class='badge badge-muted'>ทั่วไป</span>"
            }).ToList();
            gvCustomers.DataBind();

            gvEmployees.DataSource = data.Employees.Select(e => new
            {
                e.EmployeeId,
                e.Name,
                e.Position,
                SalaryDisplay = FormatCurrency(e.Salary)
            }).ToList();
            gvEmployees.DataBind();

            var categorySales = data.SalesByCategory.OrderByDescending(x => x.Value).ToList();
            var maxCategoryValue = categorySales.Count > 0 ? categorySales[0].Value : 1m;
            if (maxCategoryValue <= 0m)
            {
                maxCategoryValue = 1m;
            }

            rptCategories.DataSource = categorySales.Select(item => new CategoryChartItem
            {
                Category = item.Key,
                AmountDisplay = FormatCurrency(item.Value),
                BarStyle = string.Format(
                    CultureInfo.InvariantCulture,
                    "width: {0:0.#}%;",
                    (item.Value / maxCategoryValue) * 100m)
            }).ToList();
            rptCategories.DataBind();
        }

        private static string FormatCurrency(decimal amount)
        {
            return amount.ToString("#,##0", new CultureInfo("th-TH")) + " ฿";
        }

        private sealed class CategoryChartItem
        {
            public string Category { get; set; }
            public string AmountDisplay { get; set; }
            public string BarStyle { get; set; }
        }
    }
}
