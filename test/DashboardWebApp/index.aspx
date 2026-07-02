<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="index.aspx.cs" Inherits="DashboardWebApp.index" %>

<!DOCTYPE html>
<html lang="th">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>แดชบอร์ดร้านวัสดุก่อสร้าง | DashboardWebApp</title>
    <link rel="stylesheet" href="Content/css/dashboard.css" />
</head>
<body>
    <form id="form1" runat="server">
        <div class="dashboard-shell">
            <header class="dashboard-header">
                <div>
                    <h1>🏗️ แดชบอร์ดร้านวัสดุก่อสร้าง</h1>
                    <p>ข้อมูลจากไฟล์ Excel (test4.xlsx) ผ่าน Microsoft ACE OLEDB 12.0</p>
                </div>
                <span class="header-badge">● ออนไลน์ — ASP.NET Web Forms</span>
            </header>

            <asp:Panel ID="pnlError" runat="server" Visible="false" CssClass="alert-error">
                <asp:Label ID="lblError" runat="server" />
            </asp:Panel>

            <asp:Panel ID="pnlDashboard" runat="server">
                <section class="stats-grid">
                    <article class="stat-card blue">
                        <div class="stat-icon">💰</div>
                        <div class="stat-label">ยอดขายรวม</div>
                        <div class="stat-value"><asp:Label ID="lblTotalRevenue" runat="server" /></div>
                    </article>
                    <article class="stat-card emerald">
                        <div class="stat-icon">📦</div>
                        <div class="stat-label">จำนวนสินค้า</div>
                        <div class="stat-value"><asp:Label ID="lblTotalProducts" runat="server" /></div>
                    </article>
                    <article class="stat-card amber">
                        <div class="stat-icon">👤</div>
                        <div class="stat-label">ลูกค้า / สมาชิก</div>
                        <div class="stat-value"><asp:Label ID="lblTotalCustomers" runat="server" /></div>
                    </article>
                    <article class="stat-card violet">
                        <div class="stat-icon">👷</div>
                        <div class="stat-label">พนักงาน</div>
                        <div class="stat-value"><asp:Label ID="lblTotalEmployees" runat="server" /></div>
                    </article>
                    <article class="stat-card cyan">
                        <div class="stat-icon">🧾</div>
                        <div class="stat-label">รายการขาย</div>
                        <div class="stat-value"><asp:Label ID="lblTotalSales" runat="server" /></div>
                    </article>
                    <article class="stat-card rose">
                        <div class="stat-icon">📊</div>
                        <div class="stat-label">มูลค่าสต็อก</div>
                        <div class="stat-value"><asp:Label ID="lblStockValue" runat="server" /></div>
                    </article>
                </section>

                <section class="content-grid">
                    <article class="panel">
                        <div class="panel-header">
                            <h2>🧾 รายการขายล่าสุด</h2>
                        </div>
                        <div class="panel-body">
                            <asp:GridView ID="gvSales" runat="server" CssClass="data-table" AutoGenerateColumns="False" GridLines="None">
                                <Columns>
                                    <asp:BoundField DataField="SaleId" HeaderText="รหัส" />
                                    <asp:BoundField DataField="SaleDateDisplay" HeaderText="วันที่" />
                                    <asp:BoundField DataField="ProductId" HeaderText="สินค้า" />
                                    <asp:BoundField DataField="Quantity" HeaderText="จำนวน" ItemStyle-CssClass="text-right" HeaderStyle-CssClass="text-right" />
                                    <asp:BoundField DataField="TotalAmountDisplay" HeaderText="รวมเงิน" ItemStyle-CssClass="text-right amount-positive" HeaderStyle-CssClass="text-right" />
                                </Columns>
                                <EmptyDataTemplate>
                                    <p style="padding: 24px; color: #94a3b8;">ไม่พบข้อมูลการขาย</p>
                                </EmptyDataTemplate>
                            </asp:GridView>
                        </div>
                    </article>

                    <article class="panel">
                        <div class="panel-header">
                            <h2>📈 ยอดขายตามประเภท</h2>
                        </div>
                        <div class="panel-body">
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
                </section>

                <section class="content-grid">
                    <article class="panel">
                        <div class="panel-header">
                            <h2>🏗️ สินค้าคงเหลือ</h2>
                        </div>
                        <div class="panel-body">
                            <asp:GridView ID="gvProducts" runat="server" CssClass="data-table" AutoGenerateColumns="False" GridLines="None">
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

                    <article class="panel">
                        <div class="panel-header">
                            <h2>👤 ลูกค้า &amp; 👷 พนักงาน</h2>
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
                            <asp:GridView ID="gvEmployees" runat="server" CssClass="data-table" AutoGenerateColumns="False" GridLines="None" Style="margin-top: 8px; border-top: 1px solid rgba(148,163,184,0.15);">
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
                DashboardWebApp &mdash; ASP.NET Web Forms | ข้อมูลจาก App_Data/test4.xlsx
            </footer>
        </div>
    </form>
</body>
</html>
