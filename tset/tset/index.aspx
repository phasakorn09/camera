<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="index.aspx.cs" Inherits="tset.index" ResponseEncoding="utf-8" %>

<!DOCTYPE html>
<html lang="th">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>แดชบอร์ดร้านวัสดุก่อสร้าง | tset</title>
    <link rel="stylesheet" href="Content/css/dashboard.css" />
    <script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js"></script>
</head>
<body>
    <form id="form1" runat="server">
        <div class="dashboard-wrapper">

            <!-- Header -->
            <header class="dashboard-header">
                <div>
                    <h1>&#127959; แดชบอร์ดร้านวัสดุก่อสร้าง</h1>
                    <p>ระบบแสดงข้อมูลจากไฟล์ Excel (test4.xlsx) — ASP.NET Web Forms</p>
                </div>
                <div class="header-badge">
                    &#128197; อัปเดต: <asp:Literal ID="litLastUpdate" runat="server" />
                </div>
            </header>

            <!-- Error Panel -->
            <asp:Panel ID="pnlError" runat="server" Visible="false" CssClass="error-panel">
                <h3>&#9888; เกิดข้อผิดพลาด</h3>
                <asp:Literal ID="litError" runat="server" />
            </asp:Panel>

            <!-- Summary Cards -->
            <div class="summary-grid">
                <div class="summary-card card-blue">
                    <div class="card-icon">&#128176;</div>
                    <div class="card-label">ยอดขายรวม</div>
                    <div class="card-value"><asp:Literal ID="litTotalRevenue" runat="server" /></div>
                    <div class="card-sub"><asp:Literal ID="litTotalSales" runat="server" /> รายการ</div>
                </div>
                <div class="summary-card card-green">
                    <div class="card-icon">&#128230;</div>
                    <div class="card-label">สินค้าทั้งหมด</div>
                    <div class="card-value"><asp:Literal ID="litTotalProducts" runat="server" /></div>
                    <div class="card-sub">สต็อกต่ำ: <asp:Literal ID="litLowStock" runat="server" /> รายการ</div>
                </div>
                <div class="summary-card card-orange">
                    <div class="card-icon">&#128101;</div>
                    <div class="card-label">ลูกค้า</div>
                    <div class="card-value"><asp:Literal ID="litTotalCustomers" runat="server" /></div>
                    <div class="card-sub">สมาชิก: <asp:Literal ID="litMemberCount" runat="server" /> คน</div>
                </div>
                <div class="summary-card card-purple">
                    <div class="card-icon">&#128188;</div>
                    <div class="card-label">พนักงาน</div>
                    <div class="card-value"><asp:Literal ID="litTotalEmployees" runat="server" /></div>
                    <div class="card-sub">กำไรรวม: <asp:Literal ID="litTotalProfit" runat="server" /></div>
                </div>
            </div>

            <!-- Charts -->
            <div class="charts-grid">
                <div class="chart-card">
                    <h3>&#128200; ยอดขายรายเดือน</h3>
                    <div class="chart-container">
                        <canvas id="chartMonthlySales"></canvas>
                    </div>
                </div>
                <div class="chart-card">
                    <h3>&#127991; ยอดขายตามประเภทสินค้า</h3>
                    <div class="chart-container">
                        <canvas id="chartCategorySales"></canvas>
                    </div>
                </div>
            </div>

            <!-- Sales Table -->
            <div class="table-section">
                <h2 class="section-title">&#129534; รายการขายล่าสุด</h2>
                <div class="table-card">
                    <asp:GridView ID="gvSales" runat="server" CssClass="data-table" AutoGenerateColumns="false"
                        GridLines="None" ShowHeader="true" EmptyDataText="ไม่มีข้อมูลการขาย">
                        <Columns>
                            <asp:BoundField DataField="SaleId" HeaderText="รหัส" />
                            <asp:BoundField DataField="SaleDate" HeaderText="วันที่" DataFormatString="{0:dd/MM/yyyy}" />
                            <asp:BoundField DataField="CustomerId" HeaderText="ลูกค้า" />
                            <asp:BoundField DataField="EmployeeId" HeaderText="พนักงาน" />
                            <asp:BoundField DataField="ProductId" HeaderText="สินค้า" />
                            <asp:BoundField DataField="Quantity" HeaderText="จำนวน" ItemStyle-HorizontalAlign="Right" />
                            <asp:BoundField DataField="UnitPrice" HeaderText="ราคา/หน่วย" DataFormatString="{0:N0}" ItemStyle-HorizontalAlign="Right" />
                            <asp:BoundField DataField="TotalAmount" HeaderText="รวมเงิน" DataFormatString="{0:N0}" ItemStyle-HorizontalAlign="Right" />
                        </Columns>
                    </asp:GridView>
                </div>
            </div>

            <!-- Products Table -->
            <div class="table-section">
                <h2 class="section-title">&#128230; รายการสินค้า</h2>
                <div class="table-card">
                    <asp:GridView ID="gvProducts" runat="server" CssClass="data-table" AutoGenerateColumns="false"
                        GridLines="None" ShowHeader="true" EmptyDataText="ไม่มีข้อมูลสินค้า">
                        <Columns>
                            <asp:BoundField DataField="ProductId" HeaderText="รหัส" />
                            <asp:BoundField DataField="Name" HeaderText="ชื่อสินค้า" />
                            <asp:BoundField DataField="Category" HeaderText="ประเภท" />
                            <asp:BoundField DataField="SalePrice" HeaderText="ราคาขาย" DataFormatString="{0:N0}" ItemStyle-HorizontalAlign="Right" />
                            <asp:BoundField DataField="Cost" HeaderText="ต้นทุน" DataFormatString="{0:N0}" ItemStyle-HorizontalAlign="Right" />
                            <asp:BoundField DataField="Stock" HeaderText="คงเหลือ" ItemStyle-HorizontalAlign="Right" />
                        </Columns>
                    </asp:GridView>
                </div>
            </div>

            <!-- Customers & Employees -->
            <div class="charts-grid">
                <div class="table-section">
                    <h2 class="section-title">&#128100; ลูกค้า</h2>
                    <div class="table-card">
                        <asp:GridView ID="gvCustomers" runat="server" CssClass="data-table" AutoGenerateColumns="false"
                            GridLines="None" ShowHeader="true" EmptyDataText="ไม่มีข้อมูลลูกค้า">
                            <Columns>
                                <asp:BoundField DataField="CustomerId" HeaderText="รหัส" />
                                <asp:BoundField DataField="FirstName" HeaderText="ชื่อ" />
                                <asp:BoundField DataField="LastName" HeaderText="นามสกุล" />
                                <asp:BoundField DataField="Phone" HeaderText="เบอร์โทร" />
                                <asp:BoundField DataField="IsMember" HeaderText="สมาชิก" />
                            </Columns>
                        </asp:GridView>
                    </div>
                </div>
                <div class="table-section">
                    <h2 class="section-title">&#128188; พนักงาน</h2>
                    <div class="table-card">
                        <asp:GridView ID="gvEmployees" runat="server" CssClass="data-table" AutoGenerateColumns="false"
                            GridLines="None" ShowHeader="true" EmptyDataText="ไม่มีข้อมูลพนักงาน">
                            <Columns>
                                <asp:BoundField DataField="EmployeeId" HeaderText="รหัส" />
                                <asp:BoundField DataField="Name" HeaderText="ชื่อ" />
                                <asp:BoundField DataField="Position" HeaderText="ตำแหน่ง" />
                                <asp:BoundField DataField="Salary" HeaderText="เงินเดือน" DataFormatString="{0:N0}" ItemStyle-HorizontalAlign="Right" />
                            </Columns>
                        </asp:GridView>
                    </div>
                </div>
            </div>

            <footer class="dashboard-footer">
                tset Dashboard &mdash; ASP.NET Web Forms + Microsoft ACE OLEDB 12.0 &mdash; ข้อมูลจาก test4.xlsx
            </footer>
        </div>

        <!-- Chart Data (hidden) -->
        <asp:HiddenField ID="hfMonthlyLabels" runat="server" />
        <asp:HiddenField ID="hfMonthlyValues" runat="server" />
        <asp:HiddenField ID="hfCategoryLabels" runat="server" />
        <asp:HiddenField ID="hfCategoryValues" runat="server" />

        <script type="text/javascript">
            document.addEventListener('DOMContentLoaded', function () {
                var chartDefaults = {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: {
                        legend: {
                            labels: { color: '#94a3b8', font: { family: 'Segoe UI, Sarabun, sans-serif' } }
                        }
                    },
                    scales: {
                        x: {
                            ticks: { color: '#94a3b8' },
                            grid: { color: 'rgba(51, 65, 85, 0.5)' }
                        },
                        y: {
                            ticks: { color: '#94a3b8' },
                            grid: { color: 'rgba(51, 65, 85, 0.5)' }
                        }
                    }
                };

                var monthlyLabels = document.getElementById('<%= hfMonthlyLabels.ClientID %>').value.split('|');
                var monthlyValues = document.getElementById('<%= hfMonthlyValues.ClientID %>').value.split('|').map(Number);
                var categoryLabels = document.getElementById('<%= hfCategoryLabels.ClientID %>').value.split('|');
                var categoryValues = document.getElementById('<%= hfCategoryValues.ClientID %>').value.split('|').map(Number);

                if (monthlyLabels[0]) {
                    new Chart(document.getElementById('chartMonthlySales'), {
                        type: 'bar',
                        data: {
                            labels: monthlyLabels,
                            datasets: [{
                                label: 'ยอดขาย (บาท)',
                                data: monthlyValues,
                                backgroundColor: 'rgba(37, 99, 235, 0.7)',
                                borderColor: '#2563eb',
                                borderWidth: 1,
                                borderRadius: 8
                            }]
                        },
                        options: Object.assign({}, chartDefaults, {
                            plugins: { legend: { display: false } }
                        })
                    });
                }

                if (categoryLabels[0]) {
                    new Chart(document.getElementById('chartCategorySales'), {
                        type: 'doughnut',
                        data: {
                            labels: categoryLabels,
                            datasets: [{
                                data: categoryValues,
                                backgroundColor: [
                                    '#2563eb', '#7c3aed', '#059669', '#d97706',
                                    '#dc2626', '#0891b2', '#db2777', '#65a30d'
                                ],
                                borderWidth: 0
                            }]
                        },
                        options: {
                            responsive: true,
                            maintainAspectRatio: false,
                            plugins: {
                                legend: {
                                    position: 'right',
                                    labels: { color: '#94a3b8', font: { family: 'Segoe UI, Sarabun, sans-serif' }, padding: 12 }
                                }
                            }
                        }
                    });
                }
            });
        </script>
    </form>
</body>
</html>
