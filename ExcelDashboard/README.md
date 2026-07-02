# ExcelDashboard — ASP.NET Web Forms Dashboard

แดชบอร์ดร้านวัสดุก่อสร้าง อ่านข้อมูลจากไฟล์ Excel ด้วย **Microsoft ACE OLEDB 12.0**

## ข้อกำหนดโปรเจกต์

| รายการ | ค่า |
|--------|-----|
| Framework | .NET Framework 4.6.1 |
| ประเภท | ASP.NET Web Forms (.aspx) |
| IDE | Visual Studio 2022 / 2026 |
| IIS Express Port | **51235** |
| Start Page | **index.aspx** |
| ไฟล์ Excel | `App_Data/test4.xlsx` |
| Path โปรเจกต์ (Windows) | `C:\Users\Wiwat.L\source\repos\ExcelDashboard\` |

## โครงสร้างข้อมูลใน Excel (Sheet1)

ไฟล์ `test4.xlsx` มีข้อมูล 4 ส่วนใน Sheet เดียว:

| ส่วน | Range | คอลัมน์ |
|------|-------|---------|
| 🏗️ สินค้า | A2:F12 | ProductID, ชื่อสินค้า, ประเภท, ราคาขาย, ต้นทุน, จำนวนคงเหลือ |
| 👤 ลูกค้า | I2:M7 | CustomerID, ชื่อ, นามสกุล, เบอร์โทร, สมาชิก |
| 👷 พนักงาน | H10:K14 | EmployeeID, ชื่อ, ตำแหน่ง, เงินเดือน |
| 🧾 การขาย | A17:H27 | SaleID, วันที่, CustomerID, EmployeeID, ProductID, จำนวน, ราคาต่อหน่วย, รวมเงิน |

## วิธีรันด้วย Visual Studio 2026

### ขั้นที่ 1 — Clone โปรเจกต์ไปที่ source\repos

เปิด **PowerShell** หรือ **Command Prompt** แล้วรัน:

```powershell
cd C:\Users\Wiwat.L\source\repos
git clone -b cursor/aspnet-excel-dashboard-7d51 https://github.com/phasakorn09/camera.git ExcelDashboard
cd ExcelDashboard
```

> ถ้า clone ไว้แล้ว ให้ checkout branch:
> ```powershell
> cd C:\Users\Wiwat.L\source\repos\ExcelDashboard
> git fetch origin
> git checkout cursor/aspnet-excel-dashboard-7d51
> ```

Solution file อยู่ที่:

```
C:\Users\Wiwat.L\source\repos\ExcelDashboard\ExcelDashboard\ExcelDashboard.sln
```

### ขั้นที่ 2 — ติดตั้ง Microsoft Access Database Engine

ดาวน์โหลดและติดตั้ง **Microsoft Access Database Engine 2016 Redistributable** (ACE OLEDB 12.0):

https://www.microsoft.com/en-us/download/details.aspx?id=54920

> **สำคัญ:** ติดตั้งเวอร์ชัน **32-bit (x86)** ให้ตรงกับ Platform target ของโปรเจกต์

### ขั้นที่ 3 — เปิด Solution

1. เปิด Visual Studio 2026
2. File → Open → Project/Solution
3. เลือก `C:\Users\Wiwat.L\source\repos\ExcelDashboard\ExcelDashboard\ExcelDashboard.sln`

### ขั้นที่ 4 — ตั้งค่า Platform Target

1. คลิกขวาที่ Project **ExcelDashboard** → Properties
2. แท็บ **Build** → Platform target: **x86**
3. บันทึก

### ขั้นที่ 5 — ตรวจสอบ Start Page และ Port

- Start Page: `index.aspx` (ตั้งค่าไว้แล้วใน `.csproj`)
- IIS Express Port: **51235**
- URL: `http://localhost:51235/`

### ขั้นที่ 6 — รัน

กด **F5** หรือ **Ctrl+F5** — แดชบอร์ดจะเปิดที่ `http://localhost:51235/index.aspx`

## ฟีเจอร์แดชบอร์ด

- **KPI Cards** — ยอดขายรวม, กำไร, จำนวนสินค้า/ลูกค้า/พนักงาน, สินค้าใกล้หมด
- **กราฟแท่ง** — ยอดขายตามเดือน
- **กราฟโดนัท** — ยอดขายตามประเภทสินค้า
- **ตารางข้อมูล** — การขาย, สินค้า, ลูกค้า, พนักงาน

## โครงสร้างไฟล์

```
ExcelDashboard/
├── ExcelDashboard.sln
└── ExcelDashboard/
    ├── index.aspx              ← หน้าแดชบอร์ดหลัก
    ├── index.aspx.cs
    ├── Web.config
    ├── App_Data/
    │   └── test4.xlsx          ← ไฟล์ Excel ต้นทาง
    ├── App_Code/
    │   ├── ExcelDataService.cs ← อ่าน Excel ด้วย ACE OLEDB
    │   └── Models.cs
    └── Content/
        └── dashboard.css       ← สไตล์ UI
```

## แก้ปัญหาเบื้องต้น

| ปัญหา | วิธีแก้ |
|-------|---------|
| `Microsoft.ACE.OLEDB.12.0 provider is not registered` | ติดตั้ง Access Database Engine 2016 (x86) |
| `External table is not in the expected format` | ตรวจสอบว่าไฟล์ `.xlsx` อยู่ใน `App_Data/` |
| Port 51235 ถูกใช้งาน | เปลี่ยน port ใน `.csproj` และ `.csproj.user` |
