<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="index.aspx.cs" Inherits="ExcelDashboardWebForms.index" %>

<!DOCTYPE html>
<html lang="th">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>แดชบอร์ดร้านวัสดุก่อสร้าง | Excel Dashboard</title>
    <link rel="preconnect" href="https://fonts.googleapis.com" />
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin="anonymous" />
    <link href="https://fonts.googleapis.com/css2?family=IBM+Plex+Sans+Thai:wght@300;400;500;600;700&display=swap" rel="stylesheet" />
    <link href="Content/Site.css" rel="stylesheet" />
    <script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js"></script>
</head>
<body>
    <form id="form1" runat="server">
        <div class="page-shell">
            <aside class="sidebar">
                <div class="brand">
                    <div class="brand-icon">🏗️</div>
                    <div>
                        <h1>BuildMart</h1>
                        <p>Excel Dashboard</p>
                    </div>
                </div>
                <nav class="sidebar-nav">
                    <a href="#overview" class="nav-item active">ภาพรวม</a>
                    <a href="#sales" class="nav-item">ยอดขาย</a>
                    <a href="#products" class="nav-item">สินค้า</a>
                    <a href="#customers" class="nav-item">ลูกค้า</a>
                    <a href="#employees" class="nav-item">พนักงาน</a>
                </nav>
                <div class="sidebar-footer">
                    <p class="meta-label">แหล่งข้อมูล</p>
                    <p class="meta-value"><asp:Literal ID="litExcelFile" runat="server" /></p>
                    <p class="meta-label">โหลดล่าสุด</p>
                    <p class="meta-value"><asp:Literal ID="litLoadedAt" runat="server" /></p>
                    <asp:Button ID="btnRefresh" runat="server" Text="รีเฟรชข้อมูล" CssClass="btn-refresh" OnClick="btnRefresh_Click" />
                </div>
            </aside>

            <main class="main-content">
                <header class="topbar">
                    <div>
                        <p class="eyebrow">ASP.NET Web Forms + ACE OLEDB 12.0</p>
                        <h2>แดชบอร์ดร้านวัสดุก่อสร้าง</h2>
                    </div>
                    <div class="topbar-badge">
                        <span class="dot"></span>
                        ออนไลน์จาก test4.xlsx
                    </div>
                </header>

                <asp:Panel ID="pnlError" runat="server" Visible="false" CssClass="alert-error">
                    <strong>ไม่สามารถโหลดข้อมูลได้</strong>
                    <asp:Literal ID="litError" runat="server" />
                </asp:Panel>

                <section id="overview" class="section">
                    <div class="kpi-grid">
                        <article class="kpi-card kpi-revenue">
                            <p>ยอดขายรวม</p>
                            <h3><asp:Literal ID="litTotalRevenue" runat="server" /></h3>
                            <span>จาก <asp:Literal ID="litSaleCount" runat="server" /> รายการ</span>
                        </article>
                        <article class="kpi-card kpi-profit">
                            <p>กำไรโดยประมาณ</p>
                            <h3><asp:Literal ID="litTotalProfit" runat="server" /></h3>
                            <span>หลังหักต้นทุน</span>
                        </article>
                        <article class="kpi-card kpi-products">
                            <p>สินค้า</p>
                            <h3><asp:Literal ID="litProductCount" runat="server" /></h3>
                            <span>คงคลัง <asp:Literal ID="litTotalStock" runat="server" /> ชิ้น</span>
                        </article>
                        <article class="kpi-card kpi-customers">
                            <p>ลูกค้า</p>
                            <h3><asp:Literal ID="litCustomerCount" runat="server" /></h3>
                            <span>สมาชิก <asp:Literal ID="litMemberCount" runat="server" /> คน</span>
                        </article>
                        <article class="kpi-card kpi-employees">
                            <p>พนักงาน</p>
                            <h3><asp:Literal ID="litEmployeeCount" runat="server" /></h3>
                            <span>เฉลี่ยต่อบิล <asp:Literal ID="litAvgOrder" runat="server" /></span>
                        </article>
                        <article class="kpi-card kpi-highlight">
                            <p>Best Seller / Top Customer</p>
                            <h3 class="small-title"><asp:Literal ID="litTopProduct" runat="server" /></h3>
                            <span><asp:Literal ID="litTopCustomer" runat="server" /></span>
                        </article>
                    </div>
                </section>

                <section id="sales" class="section chart-section">
                    <div class="panel">
                        <div class="panel-header">
                            <h3>ยอดขายรายวัน</h3>
                            <p>แนวโน้มรายได้จากการขายสินค้า</p>
                        </div>
                        <div class="chart-wrap">
                            <canvas id="salesDateChart"></canvas>
                        </div>
                    </div>
                    <div class="panel">
                        <div class="panel-header">
                            <h3>ยอดขายตามสินค้า</h3>
                            <p>10 อันดับสินค้าที่มียอดขายสูงสุด</p>
                        </div>
                        <div class="chart-wrap">
                            <canvas id="salesProductChart"></canvas>
                        </div>
                    </div>
                </section>

                <section class="section chart-section">
                    <div class="panel">
                        <div class="panel-header">
                            <h3>มูลค่าคงคลังตามประเภท</h3>
                            <p>สัดส่วนมูลค์สินค้าคงเหลือ</p>
                        </div>
                        <div class="chart-wrap chart-wrap-sm">
                            <canvas id="categoryChart"></canvas>
                        </div>
                    </div>
                    <div class="panel table-panel">
                        <div class="panel-header">
                            <h3>สรุปประเภทสินค้า</h3>
                        </div>
                        <asp:GridView ID="gvCategories" runat="server" CssClass="data-table" AutoGenerateColumns="False" GridLines="None">
                            <Columns>
                                <asp:BoundField DataField="Category" HeaderText="ประเภท" />
                                <asp:BoundField DataField="ProductCount" HeaderText="จำนวน SKU" />
                                <asp:BoundField DataField="TotalStock" HeaderText="คงเหลือ" DataFormatString="{0:N0}" HtmlEncode="False" />
                                <asp:BoundField DataField="TotalValue" HeaderText="มูลค่า (บาท)" DataFormatString="{0:N0}" HtmlEncode="False" />
                            </Columns>
                        </asp:GridView>
                    </div>
                </section>

                <section id="products" class="section">
                    <div class="panel table-panel full-width">
                        <div class="panel-header">
                            <h3>🏗️ ข้อมูลสินค้า</h3>
                            <p>อ่านจาก Excel ผ่าน Microsoft ACE OLEDB 12.0</p>
                        </div>
                        <asp:GridView ID="gvProducts" runat="server" CssClass="data-table" AutoGenerateColumns="False" GridLines="None">
                            <Columns>
                                <asp:BoundField DataField="ProductId" HeaderText="รหัส" />
                                <asp:BoundField DataField="Name" HeaderText="ชื่อสินค้า" />
                                <asp:BoundField DataField="Category" HeaderText="ประเภท" />
                                <asp:BoundField DataField="SalePrice" HeaderText="ราคาขาย" DataFormatString="{0:N0}" HtmlEncode="False" />
                                <asp:BoundField DataField="Cost" HeaderText="ต้นทุน" DataFormatString="{0:N0}" HtmlEncode="False" />
                                <asp:BoundField DataField="Stock" HeaderText="คงเหลือ" DataFormatString="{0:N0}" HtmlEncode="False" />
                                <asp:BoundField DataField="ProfitMargin" HeaderText="กำไร/หน่วย" DataFormatString="{0:N0}" HtmlEncode="False" />
                            </Columns>
                        </asp:GridView>
                    </div>
                </section>

                <section id="customers" class="section two-col">
                    <div class="panel table-panel">
                        <div class="panel-header">
                            <h3>👤 ลูกค้า</h3>
                        </div>
                        <asp:GridView ID="gvCustomers" runat="server" CssClass="data-table" AutoGenerateColumns="False" GridLines="None">
                            <Columns>
                                <asp:BoundField DataField="CustomerId" HeaderText="รหัส" />
                                <asp:BoundField DataField="FullName" HeaderText="ชื่อ-นามสกุล" />
                                <asp:BoundField DataField="Phone" HeaderText="เบอร์โทร" />
                                <asp:TemplateField HeaderText="สมาชิก">
                                    <ItemTemplate>
                                        <span class='<%# (bool)Eval("IsMember") ? "badge badge-yes" : "badge badge-no" %>'>
                                            <%# (bool)Eval("IsMember") ? "Yes" : "No" %>
                                        </span>
                                    </ItemTemplate>
                                </asp:TemplateField>
                            </Columns>
                        </asp:GridView>
                    </div>

                    <div class="panel table-panel" id="employees">
                        <div class="panel-header">
                            <h3>👷 พนักงาน</h3>
                        </div>
                        <asp:GridView ID="gvEmployees" runat="server" CssClass="data-table" AutoGenerateColumns="False" GridLines="None">
                            <Columns>
                                <asp:BoundField DataField="EmployeeId" HeaderText="รหัส" />
                                <asp:BoundField DataField="Name" HeaderText="ชื่อ" />
                                <asp:BoundField DataField="Position" HeaderText="ตำแหน่ง" />
                                <asp:BoundField DataField="Salary" HeaderText="เงินเดือน" DataFormatString="{0:N0}" HtmlEncode="False" />
                            </Columns>
                        </asp:GridView>
                    </div>
                </section>

                <section class="section">
                    <div class="panel table-panel full-width">
                        <div class="panel-header">
                            <h3>🧾 การขายสินค้า</h3>
                        </div>
                        <asp:GridView ID="gvSales" runat="server" CssClass="data-table" AutoGenerateColumns="False" GridLines="None">
                            <Columns>
                                <asp:BoundField DataField="SaleId" HeaderText="SaleID" />
                                <asp:BoundField DataField="SaleDate" HeaderText="วันที่" DataFormatString="{0:dd/MM/yyyy}" HtmlEncode="False" />
                                <asp:BoundField DataField="CustomerId" HeaderText="ลูกค้า" />
                                <asp:BoundField DataField="EmployeeId" HeaderText="พนักงาน" />
                                <asp:BoundField DataField="ProductId" HeaderText="สินค้า" />
                                <asp:BoundField DataField="Quantity" HeaderText="จำนวน" DataFormatString="{0:N0}" HtmlEncode="False" />
                                <asp:BoundField DataField="UnitPrice" HeaderText="ราคา/หน่วย" DataFormatString="{0:N0}" HtmlEncode="False" />
                                <asp:BoundField DataField="TotalAmount" HeaderText="รวมเงิน" DataFormatString="{0:N0}" HtmlEncode="False" />
                            </Columns>
                        </asp:GridView>
                    </div>
                </section>
            </main>
        </div>

        <asp:HiddenField ID="hfSalesDateLabels" runat="server" />
        <asp:HiddenField ID="hfSalesDateValues" runat="server" />
        <asp:HiddenField ID="hfSalesProductLabels" runat="server" />
        <asp:HiddenField ID="hfSalesProductValues" runat="server" />
        <asp:HiddenField ID="hfCategoryLabels" runat="server" />
        <asp:HiddenField ID="hfCategoryValues" runat="server" />

        <script type="text/javascript">
            document.addEventListener('DOMContentLoaded', function () {
                var palette = ['#f97316', '#0ea5e9', '#22c55e', '#a855f7', '#ef4444', '#14b8a6', '#eab308', '#6366f1'];
                var fontFamily = "'IBM Plex Sans Thai', sans-serif";

                function parseJsonField(id) {
                    var el = document.getElementById(id);
                    if (!el || !el.value) return [];
                    try { return JSON.parse(el.value); } catch (e) { return []; }
                }

                var salesDateLabels = parseJsonField('<%= hfSalesDateLabels.ClientID %>');
                var salesDateValues = parseJsonField('<%= hfSalesDateValues.ClientID %>');
                var salesProductLabels = parseJsonField('<%= hfSalesProductLabels.ClientID %>');
                var salesProductValues = parseJsonField('<%= hfSalesProductValues.ClientID %>');
                var categoryLabels = parseJsonField('<%= hfCategoryLabels.ClientID %>');
                var categoryValues = parseJsonField('<%= hfCategoryValues.ClientID %>');

                Chart.defaults.font.family = fontFamily;
                Chart.defaults.color = '#475569';

                new Chart(document.getElementById('salesDateChart'), {
                    type: 'line',
                    data: {
                        labels: salesDateLabels,
                        datasets: [{
                            label: 'ยอดขาย (บาท)',
                            data: salesDateValues,
                            borderColor: '#f97316',
                            backgroundColor: 'rgba(249, 115, 22, 0.15)',
                            fill: true,
                            tension: 0.35,
                            pointRadius: 5,
                            pointBackgroundColor: '#fff',
                            pointBorderWidth: 2
                        }]
                    },
                    options: {
                        responsive: true,
                        maintainAspectRatio: false,
                        plugins: { legend: { display: false } },
                        scales: {
                            y: { beginAtZero: true, grid: { color: 'rgba(148,163,184,0.2)' } },
                            x: { grid: { display: false } }
                        }
                    }
                });

                new Chart(document.getElementById('salesProductChart'), {
                    type: 'bar',
                    data: {
                        labels: salesProductLabels,
                        datasets: [{
                            label: 'รายได้ (บาท)',
                            data: salesProductValues,
                            backgroundColor: palette,
                            borderRadius: 8
                        }]
                    },
                    options: {
                        indexAxis: 'y',
                        responsive: true,
                        maintainAspectRatio: false,
                        plugins: { legend: { display: false } },
                        scales: {
                            x: { beginAtZero: true, grid: { color: 'rgba(148,163,184,0.2)' } },
                            y: { grid: { display: false } }
                        }
                    }
                });

                new Chart(document.getElementById('categoryChart'), {
                    type: 'doughnut',
                    data: {
                        labels: categoryLabels,
                        datasets: [{
                            data: categoryValues,
                            backgroundColor: palette,
                            borderWidth: 0
                        }]
                    },
                    options: {
                        responsive: true,
                        maintainAspectRatio: false,
                        plugins: { legend: { position: 'bottom' } }
                    }
                });
            });
        </script>
    </form>
</body>
</html>
