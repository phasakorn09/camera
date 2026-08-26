using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.UI;
using DashboardWebApp.Services;

namespace DashboardWebApp
{
    public partial class index : Page
    {
        private const string SessionExcelPathKey = "DashboardExcelPath";
        private const int MaxUploadBytes = 10 * 1024 * 1024;

        protected void Page_Load(object sender, EventArgs e)
        {
            form1.Enctype = "multipart/form-data";
            if (!IsPostBack)
            {
                LoadDashboard();
            }
        }

        protected void btnUpload_Click(object sender, EventArgs e)
        {
            if (!fuExcel.HasFile)
            {
                ShowError("กรุณาเลือกไฟล์ .xlsx ก่อนกดปุ่มโหลดข้อมูล");
                return;
            }

            var extension = Path.GetExtension(fuExcel.FileName);
            if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                ShowError("รองรับเฉพาะไฟล์ Excel นามสกุล .xlsx");
                return;
            }

            if (fuExcel.PostedFile.ContentLength <= 0 || fuExcel.PostedFile.ContentLength > MaxUploadBytes)
            {
                ShowError("ขนาดไฟล์ต้องอยู่ระหว่าง 1 ไบต์ ถึง 10 MB");
                return;
            }

            try
            {
                var uploadsDirectory = Server.MapPath("~/App_Data/uploads");
                Directory.CreateDirectory(uploadsDirectory);
                var safeName = Path.GetFileNameWithoutExtension(fuExcel.FileName);
                foreach (var invalid in Path.GetInvalidFileNameChars())
                {
                    safeName = safeName.Replace(invalid, '_');
                }

                var savedName = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}_{1:yyyyMMddHHmmss}.xlsx",
                    string.IsNullOrWhiteSpace(safeName) ? "upload" : safeName,
                    DateTime.Now);
                var savedPath = Path.Combine(uploadsDirectory, savedName);
                fuExcel.SaveAs(savedPath);
                Session[SessionExcelPathKey] = savedPath;
                LoadDashboard();
            }
            catch (Exception ex)
            {
                ShowError("อัปโหลดไฟล์ไม่สำเร็จ: " + Server.HtmlEncode(ex.Message));
            }
        }

        protected void btnReset_Click(object sender, EventArgs e)
        {
            Session.Remove(SessionExcelPathKey);
            LoadDashboard();
        }

        protected void gvProducts_RowDataBound(object sender, System.Web.UI.WebControls.GridViewRowEventArgs e)
        {
            if (e.Row.RowType != System.Web.UI.WebControls.DataControlRowType.DataRow)
            {
                return;
            }

            var stockClass = DataBinder.Eval(e.Row.DataItem, "StockClass") as string;
            if (string.Equals(stockClass, "stock-low", StringComparison.OrdinalIgnoreCase) && e.Row.Cells.Count > 4)
            {
                e.Row.Cells[4].CssClass = "text-right stock-low";
            }
        }

        protected void btnDownloadTemplate_Click(object sender, EventArgs e)
        {
            var path = ExcelDataService.ResolveDefaultExcelPath();
            if (!File.Exists(path))
            {
                ShowError("ไม่พบไฟล์ตัวอย่าง test4.xlsx ใน App_Data");
                return;
            }

            Response.Clear();
            Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            Response.AppendHeader("Content-Disposition", "attachment; filename=test4.xlsx");
            Response.TransmitFile(path);
            Response.Flush();
            HttpContext.Current.ApplicationInstance.CompleteRequest();
        }

        private void LoadDashboard()
        {
            try
            {
                var service = new ExcelDataService();
                var excelPath = Session[SessionExcelPathKey] as string;
                if (string.IsNullOrWhiteSpace(excelPath) || !File.Exists(excelPath))
                {
                    excelPath = service.DefaultExcelPath;
                }

                var data = service.LoadDashboardData(excelPath);
                BindDashboard(data);
                pnlDashboard.Visible = true;
                pnlError.Visible = false;
            }
            catch (Exception ex)
            {
                ShowError(string.Format(
                    CultureInfo.InvariantCulture,
                    "ไม่สามารถโหลดข้อมูลจาก Excel ได้: {0}<br /><br />ตรวจสอบว่าไฟล์เป็น .xlsx (Office Open XML) และมีชีต สินค้า / ลูกค้า / พนักงาน / การขาย",
                    Server.HtmlEncode(ex.Message)));
            }
        }

        private void ShowError(string htmlMessage)
        {
            pnlDashboard.Visible = false;
            pnlError.Visible = true;
            lblError.Text = htmlMessage;
        }

        private void BindDashboard(Models.DashboardData data)
        {
            lblTotalRevenue.Text = FormatCurrency(data.TotalRevenue);
            lblTotalProducts.Text = data.TotalProducts.ToString("N0", new CultureInfo("th-TH"));
            lblTotalCustomers.Text = string.Format(
                CultureInfo.InvariantCulture,
                "{0} / {1}",
                data.TotalCustomers,
                data.MemberCount);
            lblTotalEmployees.Text = data.TotalEmployees.ToString("N0", new CultureInfo("th-TH"));
            lblTotalSales.Text = data.TotalSalesRecords.ToString("N0", new CultureInfo("th-TH"));
            lblStockValue.Text = FormatCurrency(data.TotalStockValue);
            lblSourceFile.Text = data.SourceFileName;
            lblLoadedAt.Text = data.LoadedAt.ToString("dd MMM yyyy HH:mm น.", new CultureInfo("th-TH"));

            var productNames = data.Products.ToDictionary(p => p.ProductId, p => p.Name);
            var customerNames = data.Customers.ToDictionary(c => c.CustomerId, c => c.FullName);

            gvSales.DataSource = data.Sales
                .OrderByDescending(s => s.SaleDate)
                .ThenByDescending(s => s.SaleId)
                .Select(s => new
                {
                    s.SaleId,
                    SaleDateDisplay = s.SaleDate == DateTime.MinValue
                        ? "-"
                        : s.SaleDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                    CustomerName = customerNames.ContainsKey(s.CustomerId) ? customerNames[s.CustomerId] : s.CustomerId,
                    ProductName = productNames.ContainsKey(s.ProductId) ? productNames[s.ProductId] : s.ProductId,
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
                StockDisplay = p.Stock.ToString("N0", new CultureInfo("th-TH")),
                StockClass = p.Stock < 50 ? "stock-low" : "stock-ok"
            }).ToList();
            gvProducts.DataBind();

            gvLowStock.DataSource = data.LowStockProducts.Select(p => new
            {
                p.ProductId,
                p.Name,
                StockDisplay = p.Stock.ToString("N0", new CultureInfo("th-TH"))
            }).ToList();
            gvLowStock.DataBind();
            pnlLowStockEmpty.Visible = data.LowStockProducts.Count == 0;
            gvLowStock.Visible = data.LowStockProducts.Count > 0;

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

            gvEmployees.DataSource = data.Employees.Select(emp => new
            {
                emp.EmployeeId,
                emp.Name,
                emp.Position,
                SalaryDisplay = FormatCurrency(emp.Salary)
            }).ToList();
            gvEmployees.DataBind();

            var serializer = new JavaScriptSerializer();
            var categorySales = data.SalesByCategory.OrderByDescending(item => item.Value).ToList();
            litCategoryJson.Text = serializer.Serialize(new
            {
                labels = categorySales.Select(item => item.Key).ToArray(),
                values = categorySales.Select(item => item.Value).ToArray()
            });

            var stockCategories = data.StockByCategory.OrderByDescending(item => item.Value).ToList();
            litStockJson.Text = serializer.Serialize(new
            {
                labels = stockCategories.Select(item => item.Key).ToArray(),
                values = stockCategories.Select(item => item.Value).ToArray()
            });

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
            return amount.ToString("#,##0.##", new CultureInfo("th-TH")) + " ฿";
        }

        private sealed class CategoryChartItem
        {
            public string Category { get; set; }
            public string AmountDisplay { get; set; }
            public string BarStyle { get; set; }
        }
    }
}
