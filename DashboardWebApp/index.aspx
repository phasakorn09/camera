<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="index.aspx.cs" Inherits="DashboardWebApp.index" %>

<!DOCTYPE html>
<html lang="th">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>แดชบอร์ดร้านวัสดุก่อสร้าง | DashboardWebApp</title>
    <link rel="preconnect" href="https://fonts.googleapis.com" />
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
    <link href="https://fonts.googleapis.com/css2?family=Prompt:wght@400;500;600;700&amp;family=Sarabun:wght@400;500;600;700&amp;display=swap" rel="stylesheet" />
    <link rel="stylesheet" href="Content/css/dashboard.css" />
</head>
<body>
    <form id="form1" runat="server" enctype="multipart/form-data">
        <div class="app-layout">
            <aside class="sidebar">
                <div class="brand">
                    <span class="brand-mark" aria-hidden="true">
                        <svg viewBox="0 0 48 48" width="36" height="36">
                            <rect x="4" y="18" width="40" height="22" rx="3" fill="#f5a524"></rect>
                            <polygon points="4,18 24,6 44,18" fill="#f8d57a"></polygon>
                            <rect x="20" y="26" width="8" height="14" fill="#122033"></rect>
                        </svg>
                    </span>
                    <div>
                        <strong>BuildBoard</strong>
                        <span>ร้านวัสดุก่อสร้าง</span>
                    </div>
                </div>
                <nav class="side-nav">
                    <a href="#overview" class="is-active">ภาพรวม</a>
                    <a href="#sales">การขาย</a>
                    <a href="#inventory">สินค้าคงเหลือ</a>
                    <a href="#people">ลูกค้า &amp; พนักงาน</a>
                </nav>
                <div class="sidebar-foot">
                    <p>ASP.NET Web Forms</p>
                    <p>อ่านไฟล์ .xlsx ด้วย Open XML</p>
                </div>
            </aside>

            <div class="main-column">
                <header class="topbar">
                    <div>
                        <p class="eyebrow">Excel Dashboard</p>
                        <h1>แดชบอร์ดร้านวัสดุก่อสร้าง</h1>
                    </div>
                    <div class="topbar-meta">
                        <span class="status-pill">ออนไลน์</span>
                        <span class="file-chip">
                            <asp:Label ID="lblSourceFile" runat="server" />
                        </span>
                        <span class="time-chip">
                            อัปเดต <asp:Label ID="lblLoadedAt" runat="server" />
                        </span>
                    </div>
                </header>

                <section class="toolbar" aria-label="อัปโหลด Excel">
                    <div class="toolbar-copy">
                        <h2>โหลดข้อมูลจากไฟล์ Excel</h2>
                        <p>เลือกไฟล์ .xlsx ที่มีชีต สินค้า, ลูกค้า, พนักงาน และการขาย หรือใช้ไฟล์ตัวอย่าง test4.xlsx</p>
                    </div>
                    <div class="toolbar-actions">
                        <label class="file-picker">
                            <asp:FileUpload ID="fuExcel" runat="server" CssClass="file-input" accept=".xlsx" />
                            <span>เลือกไฟล์ .xlsx</span>
                        </label>
                        <asp:Button ID="btnUpload" runat="server" Text="โหลดข้อมูล" CssClass="btn btn-primary" OnClick="btnUpload_Click" />
                        <asp:Button ID="btnReset" runat="server" Text="ใช้ไฟล์ตัวอย่าง" CssClass="btn btn-ghost" OnClick="btnReset_Click" />
                        <asp:Button ID="btnDownloadTemplate" runat="server" Text="ดาวน์โหลดตัวอย่าง" CssClass="btn btn-ghost" OnClick="btnDownloadTemplate_Click" />
                    </div>
                </section>

                <asp:Panel ID="pnlError" runat="server" Visible="false" CssClass="alert-error">
                    <asp:Label ID="lblError" runat="server" />
                </asp:Panel>

                <asp:Panel ID="pnlDashboard" runat="server">
                    <section id="overview" class="stats-grid">
                        <article class="stat-card blue">
                            <div class="stat-icon">ยอดขาย</div>
                            <div class="stat-label">ยอดขายรวม</div>
                            <div class="stat-value"><asp:Label ID="lblTotalRevenue" runat="server" /></div>
                        </article>
                        <article class="stat-card emerald">
                            <div class="stat-icon">สินค้า</div>
                            <div class="stat-label">รายการสินค้า</div>
                            <div class="stat-value"><asp:Label ID="lblTotalProducts" runat="server" /></div>
                        </article>
                        <article class="stat-card amber">
                            <div class="stat-icon">ลูกค้า</div>
                            <div class="stat-label">ลูกค้า / สมาชิก</div>
                            <div class="stat-value"><asp:Label ID="lblTotalCustomers" runat="server" /></div>
                        </article>
                        <article class="stat-card violet">
                            <div class="stat-icon">ทีม</div>
                            <div class="stat-label">พนักงาน</div>
                            <div class="stat-value"><asp:Label ID="lblTotalEmployees" runat="server" /></div>
                        </article>
                        <article class="stat-card cyan">
                            <div class="stat-icon">บิล</div>
                            <div class="stat-label">รายการขาย</div>
                            <div class="stat-value"><asp:Label ID="lblTotalSales" runat="server" /></div>
                        </article>
                        <article class="stat-card rose">
                            <div class="stat-icon">สต็อก</div>
                            <div class="stat-label">มูลค่าสต็อก</div>
                            <div class="stat-value"><asp:Label ID="lblStockValue" runat="server" /></div>
                        </article>
                    </section>

                    <section class="content-grid charts-grid">
                        <article class="panel">
                            <div class="panel-header">
                                <h2>ยอดขายตามประเภทสินค้า</h2>
                            </div>
                            <div class="panel-body chart-body">
                                <canvas id="categoryChart" height="220"></canvas>
                                <asp:Repeater ID="rptCategories" runat="server">
                                    <HeaderTemplate>
                                        <ul class="category-list">
                                    </HeaderTemplate>
                                    <ItemTemplate>
                                        <li class="category-item">
                                            <span><%# Eval("Category") %></span>
                                            <div class="category-bar-wrap">
                                                <div class="category-bar" style='<%# Eval("BarStyle") %>'></div>
                                            </div>
                                            <span class="category-amount"><%# Eval("AmountDisplay") %></span>
                                        </li>
                                    </ItemTemplate>
                                    <FooterTemplate>
                                        </ul>
                                    </FooterTemplate>
                                </asp:Repeater>
                            </div>
                        </article>
                        <article class="panel">
                            <div class="panel-header">
                                <h2>สัดส่วนสต็อกตามประเภท</h2>
                            </div>
                            <div class="panel-body chart-body chart-body-donut">
                                <canvas id="stockChart" height="220"></canvas>
                            </div>
                        </article>
                    </section>

                    <section id="sales" class="content-grid">
                        <article class="panel">
                            <div class="panel-header">
                                <h2>รายการขายล่าสุด</h2>
                            </div>
                            <div class="panel-body">
                                <asp:GridView ID="gvSales" runat="server" CssClass="data-table" AutoGenerateColumns="False" GridLines="None">
                                    <Columns>
                                        <asp:BoundField DataField="SaleId" HeaderText="รหัส" />
                                        <asp:BoundField DataField="SaleDateDisplay" HeaderText="วันที่" />
                                        <asp:BoundField DataField="CustomerName" HeaderText="ลูกค้า" />
                                        <asp:BoundField DataField="ProductName" HeaderText="สินค้า" />
                                        <asp:BoundField DataField="Quantity" HeaderText="จำนวน" ItemStyle-CssClass="text-right" HeaderStyle-CssClass="text-right" />
                                        <asp:BoundField DataField="TotalAmountDisplay" HeaderText="รวมเงิน" ItemStyle-CssClass="text-right amount-positive" HeaderStyle-CssClass="text-right" />
                                    </Columns>
                                    <EmptyDataTemplate>
                                        <p class="empty-copy">ไม่พบข้อมูลการขาย</p>
                                    </EmptyDataTemplate>
                                </asp:GridView>
                            </div>
                        </article>

                        <article class="panel">
                            <div class="panel-header">
                                <h2>สินค้าใกล้หมด</h2>
                            </div>
                            <div class="panel-body">
                                <asp:Panel ID="pnlLowStockEmpty" runat="server" Visible="false">
                                    <p class="empty-copy">ไม่มีสินค้าที่สต็อกต่ำกว่า 50 ชิ้น</p>
                                </asp:Panel>
                                <asp:GridView ID="gvLowStock" runat="server" CssClass="data-table" AutoGenerateColumns="False" GridLines="None">
                                    <Columns>
                                        <asp:BoundField DataField="ProductId" HeaderText="รหัส" />
                                        <asp:BoundField DataField="Name" HeaderText="ชื่อสินค้า" />
                                        <asp:BoundField DataField="StockDisplay" HeaderText="คงเหลือ" ItemStyle-CssClass="text-right stock-low" HeaderStyle-CssClass="text-right" />
                                    </Columns>
                                </asp:GridView>
                            </div>
                        </article>
                    </section>

                    <section id="inventory" class="content-grid single">
                        <article class="panel">
                            <div class="panel-header">
                                <h2>สินค้าคงเหลือ</h2>
                            </div>
                            <div class="panel-body">
                                <asp:GridView ID="gvProducts" runat="server" CssClass="data-table" AutoGenerateColumns="False" GridLines="None" OnRowDataBound="gvProducts_RowDataBound">
                                    <Columns>
                                        <asp:BoundField DataField="ProductId" HeaderText="รหัส" />
                                        <asp:BoundField DataField="Name" HeaderText="ชื่อสินค้า" />
                                        <asp:BoundField DataField="Category" HeaderText="ประเภท" />
                                        <asp:BoundField DataField="SalePriceDisplay" HeaderText="ราคา" ItemStyle-CssClass="text-right" HeaderStyle-CssClass="text-right" />
                                        <asp:BoundField DataField="StockDisplay" HeaderText="คงเหลือ" ItemStyle-CssClass="text-right" HeaderStyle-CssClass="text-right" />
                                    </Columns>
                                </asp:GridView>
                            </div>
                        </article>
                    </section>

                    <section id="people" class="content-grid">
                        <article class="panel">
                            <div class="panel-header">
                                <h2>ลูกค้า</h2>
                            </div>
                            <div class="panel-body">
                                <asp:GridView ID="gvCustomers" runat="server" CssClass="data-table" AutoGenerateColumns="False" GridLines="None">
                                    <Columns>
                                        <asp:BoundField DataField="CustomerId" HeaderText="รหัส" />
                                        <asp:BoundField DataField="FullName" HeaderText="ชื่อ-นามสกุล" />
                                        <asp:BoundField DataField="Phone" HeaderText="เบอร์โทร" />
                                        <asp:BoundField DataField="MemberBadge" HeaderText="สมาชิก" HtmlEncode="False" />
                                    </Columns>
                                </asp:GridView>
                            </div>
                        </article>
                        <article class="panel">
                            <div class="panel-header">
                                <h2>พนักงาน</h2>
                            </div>
                            <div class="panel-body">
                                <asp:GridView ID="gvEmployees" runat="server" CssClass="data-table" AutoGenerateColumns="False" GridLines="None">
                                    <Columns>
                                        <asp:BoundField DataField="EmployeeId" HeaderText="รหัส" />
                                        <asp:BoundField DataField="Name" HeaderText="ชื่อ" />
                                        <asp:BoundField DataField="Position" HeaderText="ตำแหน่ง" />
                                        <asp:BoundField DataField="SalaryDisplay" HeaderText="เงินเดือน" ItemStyle-CssClass="text-right" HeaderStyle-CssClass="text-right" />
                                    </Columns>
                                </asp:GridView>
                            </div>
                        </article>
                    </section>
                </asp:Panel>

                <footer class="dashboard-footer">
                    DashboardWebApp — ASP.NET Web Forms · หน้าเริ่มต้น index.aspx · Visual Studio 2026
                </footer>
            </div>
        </div>

        <script type="application/json" id="category-chart-data"><asp:Literal ID="litCategoryJson" runat="server" /></script>
        <script type="application/json" id="stock-chart-data"><asp:Literal ID="litStockJson" runat="server" /></script>
        <script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js"></script>
        <script src="Content/js/dashboard.js"></script>
    </form>
</body>
</html>
