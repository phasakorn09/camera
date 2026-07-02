<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="index.aspx.cs" Inherits="ExcelDashboard.index" %>

<!DOCTYPE html>
<html lang="th">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>แดชบอร์ดร้านวัสดุก่อสร้าง</title>
    <link rel="stylesheet" href="Content/dashboard.css" />
    <script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js"></script>
</head>
<body>
    <form id="form1" runat="server">
        <div class="dashboard-wrapper">
            <header class="dashboard-header">
                <div>
                    <h1>🏗️ แดชบอร์ดร้านวัสดุก่อสร้าง</h1>
                    <p class="subtitle">ข้อมูลจากไฟล์ Excel — Microsoft ACE OLEDB 12.0</p>
                </div>
                <div class="header-badge">
                    <span class="dot"></span>
                    <asp:Label ID="lblStatus" runat="server" Text="Live Data" />
                </div>
            </header>

            <asp:Panel ID="pnlError" runat="server" Visible="false" CssClass="alert-error">
                <asp:Label ID="lblError" runat="server" />
            </asp:Panel>

            <!-- KPI Cards -->
            <div class="kpi-grid">
                <div class="kpi-card blue">
                    <div class="kpi-icon">💰</div>
                    <div class="kpi-label">ยอดขายรวม</div>
                    <div class="kpi-value currency"><asp:Label ID="lblTotalRevenue" runat="server" Text="0" /></div>
                </div>
                <div class="kpi-card green">
                    <div class="kpi-icon">📈</div>
                    <div class="kpi-label">กำไรโดยประมาณ</div>
                    <div class="kpi-value currency"><asp:Label ID="lblTotalProfit" runat="server" Text="0" /></div>
                </div>
                <div class="kpi-card purple">
                    <div class="kpi-icon">📦</div>
                    <div class="kpi-label">สินค้าทั้งหมด</div>
                    <div class="kpi-value"><asp:Label ID="lblProductCount" runat="server" Text="0" /></div>
                </div>
                <div class="kpi-card orange">
                    <div class="kpi-icon">👤</div>
                    <div class="kpi-label">ลูกค้า</div>
                    <div class="kpi-value"><asp:Label ID="lblCustomerCount" runat="server" Text="0" /></div>
                </div>
                <div class="kpi-card cyan">
                    <div class="kpi-icon">👷</div>
                    <div class="kpi-label">พนักงาน</div>
                    <div class="kpi-value"><asp:Label ID="lblEmployeeCount" runat="server" Text="0" /></div>
                </div>
                <div class="kpi-card red">
                    <div class="kpi-icon">⚠️</div>
                    <div class="kpi-label">สินค้าใกล้หมด</div>
                    <div class="kpi-value"><asp:Label ID="lblLowStock" runat="server" Text="0" /></div>
                </div>
            </div>

            <!-- Charts -->
            <div class="charts-grid">
                <div class="chart-card">
                    <h3>📊 ยอดขายตามเดือน</h3>
                    <div class="chart-container">
                        <canvas id="chartSalesByMonth"></canvas>
                    </div>
                </div>
                <div class="chart-card">
                    <h3>🏷️ ยอดขายตามประเภทสินค้า</h3>
                    <div class="chart-container">
                        <canvas id="chartSalesByCategory"></canvas>
                    </div>
                </div>
            </div>

            <!-- Sales Table -->
            <h2 class="section-title">🧾 รายการขายล่าสุด</h2>
            <div class="table-card" style="margin-bottom: 32px;">
                <div class="table-scroll">
                    <asp:GridView ID="gvSales" runat="server" CssClass="data-table" AutoGenerateColumns="False"
                        GridLines="None" ShowHeader="True" EmptyDataText="ไม่พบข้อมูลการขาย">
                        <Columns>
                            <asp:BoundField DataField="SaleId" HeaderText="Sale ID" />
                            <asp:BoundField DataField="SaleDateText" HeaderText="วันที่" />
                            <asp:BoundField DataField="CustomerId" HeaderText="ลูกค้า" />
                            <asp:BoundField DataField="EmployeeId" HeaderText="พนักงาน" />
                            <asp:BoundField DataField="ProductId" HeaderText="สินค้า" />
                            <asp:BoundField DataField="Quantity" HeaderText="จำนวน" ItemStyle-CssClass="text-center" HeaderStyle-CssClass="text-center" />
                            <asp:BoundField DataField="UnitPriceText" HeaderText="ราคา/หน่วย" ItemStyle-CssClass="text-right" HeaderStyle-CssClass="text-right" />
                            <asp:BoundField DataField="TotalText" HeaderText="รวมเงิน" ItemStyle-CssClass="text-right" HeaderStyle-CssClass="text-right" />
                        </Columns>
                    </asp:GridView>
                </div>
            </div>

            <!-- Products & Customers -->
            <div class="tables-grid">
                <div class="table-card">
                    <div class="table-card-header">📦 สินค้า</div>
                    <div class="table-scroll">
                        <asp:GridView ID="gvProducts" runat="server" CssClass="data-table" AutoGenerateColumns="False"
                            GridLines="None" ShowHeader="True" EmptyDataText="ไม่พบข้อมูลสินค้า">
                            <Columns>
                                <asp:BoundField DataField="ProductId" HeaderText="ID" />
                                <asp:BoundField DataField="Name" HeaderText="ชื่อสินค้า" />
                                <asp:BoundField DataField="Category" HeaderText="ประเภท" />
                                <asp:BoundField DataField="SalePriceText" HeaderText="ราคา" ItemStyle-CssClass="text-right" HeaderStyle-CssClass="text-right" />
                                <asp:BoundField DataField="Stock" HeaderText="คงเหลือ" ItemStyle-CssClass="text-center" HeaderStyle-CssClass="text-center" />
                                <asp:TemplateField HeaderText="สถานะ">
                                    <ItemTemplate>
                                        <span class='<%# (int)Eval("Stock") < 100 ? "badge badge-low" : "badge badge-ok" %>'>
                                            <%# (int)Eval("Stock") < 100 ? "ใกล้หมด" : "ปกติ" %>
                                        </span>
                                    </ItemTemplate>
                                </asp:TemplateField>
                            </Columns>
                        </asp:GridView>
                    </div>
                </div>

                <div class="table-card">
                    <div class="table-card-header">👤 ลูกค้า</div>
                    <div class="table-scroll">
                        <asp:GridView ID="gvCustomers" runat="server" CssClass="data-table" AutoGenerateColumns="False"
                            GridLines="None" ShowHeader="True" EmptyDataText="ไม่พบข้อมูลลูกค้า">
                            <Columns>
                                <asp:BoundField DataField="CustomerId" HeaderText="ID" />
                                <asp:BoundField DataField="FullName" HeaderText="ชื่อ-นามสกุล" />
                                <asp:BoundField DataField="Phone" HeaderText="เบอร์โทร" />
                                <asp:TemplateField HeaderText="สมาชิก">
                                    <ItemTemplate>
                                        <span class='<%# Eval("Member").ToString() == "Yes" ? "badge badge-yes" : "badge badge-no" %>'>
                                            <%# Eval("Member").ToString() == "Yes" ? "สมาชิก" : "ทั่วไป" %>
                                        </span>
                                    </ItemTemplate>
                                </asp:TemplateField>
                            </Columns>
                        </asp:GridView>
                    </div>
                </div>
            </div>

            <!-- Employees -->
            <h2 class="section-title">👷 พนักงาน</h2>
            <div class="table-card" style="margin-bottom: 32px;">
                <div class="table-scroll">
                    <asp:GridView ID="gvEmployees" runat="server" CssClass="data-table" AutoGenerateColumns="False"
                        GridLines="None" ShowHeader="True" EmptyDataText="ไม่พบข้อมูลพนักงาน">
                        <Columns>
                            <asp:BoundField DataField="EmployeeId" HeaderText="ID" />
                            <asp:BoundField DataField="Name" HeaderText="ชื่อ" />
                            <asp:BoundField DataField="Position" HeaderText="ตำแหน่ง" />
                            <asp:BoundField DataField="SalaryText" HeaderText="เงินเดือน" ItemStyle-CssClass="text-right" HeaderStyle-CssClass="text-right" />
                        </Columns>
                    </asp:GridView>
                </div>
            </div>

            <footer class="dashboard-footer">
                ExcelDashboard &mdash; ASP.NET Web Forms (.NET Framework 4.6.1) &mdash; อ่านข้อมูลจาก test4.xlsx
            </footer>
        </div>

        <asp:HiddenField ID="hfChartData" runat="server" />
    </form>

    <script type="text/javascript">
        document.addEventListener('DOMContentLoaded', function () {
            var dataField = document.getElementById('<%= hfChartData.ClientID %>');
            if (!dataField || !dataField.value) return;

            var chartData = JSON.parse(dataField.value);
            var chartDefaults = {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        labels: { color: '#94a3b8', font: { family: "'Segoe UI', 'Sarabun', sans-serif" } }
                    }
                }
            };

            new Chart(document.getElementById('chartSalesByMonth'), {
                type: 'bar',
                data: {
                    labels: chartData.salesByMonth.labels,
                    datasets: [{
                        label: 'ยอดขาย (฿)',
                        data: chartData.salesByMonth.values,
                        backgroundColor: 'rgba(59, 130, 246, 0.7)',
                        borderColor: '#3b82f6',
                        borderWidth: 1,
                        borderRadius: 8
                    }]
                },
                options: Object.assign({}, chartDefaults, {
                    scales: {
                        x: { ticks: { color: '#64748b' }, grid: { color: 'rgba(148,163,184,0.1)' } },
                        y: { ticks: { color: '#64748b' }, grid: { color: 'rgba(148,163,184,0.1)' } }
                    }
                })
            });

            new Chart(document.getElementById('chartSalesByCategory'), {
                type: 'doughnut',
                data: {
                    labels: chartData.salesByCategory.labels,
                    datasets: [{
                        data: chartData.salesByCategory.values,
                        backgroundColor: [
                            '#3b82f6', '#8b5cf6', '#10b981', '#f59e0b',
                            '#ef4444', '#06b6d4', '#ec4899', '#84cc16'
                        ],
                        borderWidth: 0
                    }]
                },
                options: chartDefaults
            });
        });
    </script>
</body>
</html>
