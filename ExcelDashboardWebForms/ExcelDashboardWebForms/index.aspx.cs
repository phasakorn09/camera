using System;
using System.Linq;
using System.Web.Script.Serialization;
using System.Web.UI;

namespace ExcelDashboardWebForms
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

        protected void btnRefresh_Click(object sender, EventArgs e)
        {
            LoadDashboard();
        }

        private void LoadDashboard()
        {
            pnlError.Visible = false;

            try
            {
                var service = new ExcelDataService();
                var data = service.LoadDashboard();
                BindSummary(data);
                BindGrids(data);
                BindChartData(data);
            }
            catch (Exception ex)
            {
                pnlError.Visible = true;
                litError.Text = Server.HtmlEncode(ex.Message);
            }
        }

        private void BindSummary(DashboardData data)
        {
            var summary = data.Summary;

            litExcelFile.Text = "App_Data/test4.xlsx";
            litLoadedAt.Text = data.LoadedAt.ToString("dd/MM/yyyy HH:mm:ss");
            litTotalRevenue.Text = FormatCurrency(summary.TotalRevenue);
            litTotalProfit.Text = FormatCurrency(summary.TotalProfit);
            litProductCount.Text = summary.ProductCount.ToString("N0");
            litTotalStock.Text = summary.TotalStock.ToString("N0");
            litCustomerCount.Text = summary.CustomerCount.ToString("N0");
            litMemberCount.Text = summary.MemberCount.ToString("N0");
            litEmployeeCount.Text = summary.EmployeeCount.ToString("N0");
            litSaleCount.Text = summary.SaleCount.ToString("N0");
            litAvgOrder.Text = FormatCurrency(summary.AverageOrderValue);
            litTopProduct.Text = summary.TopProduct;
            litTopCustomer.Text = summary.TopCustomer;
        }

        private void BindGrids(DashboardData data)
        {
            gvProducts.DataSource = data.Products;
            gvProducts.DataBind();

            gvCustomers.DataSource = data.Customers;
            gvCustomers.DataBind();

            gvEmployees.DataSource = data.Employees;
            gvEmployees.DataBind();

            gvSales.DataSource = data.Sales;
            gvSales.DataBind();

            gvCategories.DataSource = data.CategorySummaries;
            gvCategories.DataBind();
        }

        private void BindChartData(DashboardData data)
        {
            var serializer = new JavaScriptSerializer();

            hfSalesDateLabels.Value = serializer.Serialize(data.SalesByDate.Select(x => x.DateLabel).ToList());
            hfSalesDateValues.Value = serializer.Serialize(data.SalesByDate.Select(x => x.Revenue).ToList());

            var topProducts = data.SalesByProduct.Take(10).ToList();
            hfSalesProductLabels.Value = serializer.Serialize(topProducts.Select(x => Truncate(x.ProductName, 28)).ToList());
            hfSalesProductValues.Value = serializer.Serialize(topProducts.Select(x => x.Revenue).ToList());

            hfCategoryLabels.Value = serializer.Serialize(data.CategorySummaries.Select(x => x.Category).ToList());
            hfCategoryValues.Value = serializer.Serialize(data.CategorySummaries.Select(x => x.TotalValue).ToList());
        }

        private static string FormatCurrency(decimal amount)
        {
            return amount.ToString("N0") + " ฿";
        }

        private static string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            {
                return text;
            }

            return text.Substring(0, maxLength - 1) + "…";
        }
    }
}
