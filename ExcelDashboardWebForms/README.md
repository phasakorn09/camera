# Excel Dashboard Web Forms

โปรเจกต์ **ASP.NET Web Forms ใหม่ทั้งหมด** สำหรับแสดงแดชบอร์ดร้านวัสดุก่อสร้าง โดยอ่านข้อมูลจากไฟล์ Excel `test4.xlsx` ผ่าน **Microsoft ACE OLEDB 12.0**

> โปรเจกต์นี้แยกจากโปรเจกต์เดิมใน repository โดยสิ้นเชิง อยู่ในโฟลเดอร์ `ExcelDashboardWebForms/` เท่านั้น

## โครงสร้างข้อมูลใน Excel (test4.xlsx)

ไฟล์ใช้ Sheet เดียว (`Sheet1`) แต่มีหลายตารางใน layout เดียวกัน:

| ส่วนข้อมูล | ตำแหน่ง | คอลัมน์หลัก |
|-----------|---------|-------------|
| สินค้า | แถว 2–12, คอลัมน์ A–F | ProductID, ชื่อสินค้า, ประเภท, ราคาขาย, ต้นทุน, จำนวนคงเหลือ |
| ลูกค้า | แถว 2–7, คอลัมน์ I–M | CustomerID, ชื่อ, นามสกุล, เบอร์โทร, สมาชิก |
| พนักงาน | แถว 11–14, คอลัมน์ H–K | EmployeeID, ชื่อ, ตำแหน่ง, เงินเดือน |
| การขาย | แถว 17–27, คอลัมน์ A–H | SaleID, วันที่, CustomerID, EmployeeID, ProductID, จำนวน, ราคาต่อหน่วย, รวมเงิน |

## โครงสร้างโปรเจกต์

```
ExcelDashboardWebForms/
├── ExcelDashboardWebForms.sln
└── ExcelDashboardWebForms/
    ├── index.aspx              ← หน้าเริ่มต้น
    ├── index.aspx.cs
    ├── Web.config
    ├── Global.asax
    ├── App_Data/
    │   └── test4.xlsx          ← ฐานข้อมูล Excel
    ├── App_Code/
    │   ├── ExcelDataService.cs ← อ่าน Excel ด้วย ACE OLEDB
    │   └── Models.cs
    └── Content/
        └── Site.css
```

## ความต้องการของระบบ

- Windows 10/11
- Visual Studio 2022 หรือ **Visual Studio 2026**
- .NET Framework 4.8
- **Microsoft Access Database Engine 2016 Redistributable** (ACE OLEDB 12.0/16.0)
  - ดาวน์โหลด: https://www.microsoft.com/en-us/download/details.aspx?id=54920
  - ติดตั้งเวอร์ชัน **64-bit** หากใช้ IIS Express 64-bit

## ขั้นที่ 5 — วิธีรันด้วย Visual Studio

### 1) คัดลอกโปรเจกต์ไปที่ C:\

```text
C:\ExcelDashboardWebForms\
```

### 2) เปิด Solution

1. เปิด Visual Studio 2026
2. File → Open → Project/Solution
3. เลือก `C:\ExcelDashboardWebForms\ExcelDashboardWebForms.sln`

### 3) ตั้งค่า Platform (สำคัญสำหรับ ACE OLEDB)

- ไปที่ Build → Configuration Manager
- ตั้ง Platform เป็น **x64** หรือ **Any CPU** (ปิด Prefer 32-bit)
- Project Properties → Build → ยกเลิก **Prefer 32-bit**

### 4) ตรวจสอบไฟล์ Excel

- วางไฟล์ `test4.xlsx` ที่ `App_Data\test4.xlsx`
- หรือแก้ชื่อไฟล์ใน `Web.config`:

```xml
<add key="ExcelFileName" value="test4.xlsx" />
```

### 5) รันโปรเจกต์

1. ตั้ง `index.aspx` เป็นหน้าเริ่มต้น (ตั้งไว้แล้วใน Web.config)
2. กด **F5** หรือ IIS Express
3. เปิดเบราว์เซอร์ที่ `http://localhost:5050/index.aspx`

## การทำงานของระบบอ่าน Excel

Connection String ที่ใช้:

```csharp
Provider=Microsoft.ACE.OLEDB.12.0;
Data Source={path}\App_Data\test4.xlsx;
Extended Properties="Excel 12.0 Xml;HDR=NO;IMEX=1";
```

`ExcelDataService` จะ:

1. อ่านทุกแถวจาก `[Sheet1$]`
2. แยกข้อมูลสินค้า / ลูกค้า / พนักงาน / การขาย ตามรูปแบบในไฟล์
3. คำนวณ KPI, กราฟ, และตารางสำหรับแดชบอร์ด

## ฟีเจอร์แดชบอร์ด

- KPI Cards: ยอดขาย, กำไร, สินค้า, ลูกค้า, พนักงาน
- กราฟยอดขายรายวัน (Line Chart)
- กราฟยอดขายตามสินค้า (Bar Chart)
- กราฟมูลค่าคงคลังตามประเภท (Doughnut Chart)
- ตารางสินค้า, ลูกค้า, พนักงาน, การขาย
- ปุ่มรีเฟรชข้อมูลจาก Excel

## แก้ปัญหาเบื้องต้น

| ปัญหา | วิธีแก้ |
|-------|--------|
| `Microsoft.ACE.OLEDB.12.0 provider is not registered` | ติดตั้ง Access Database Engine และใช้ Platform x64 |
| อ่าน Excel ไม่ได้ | ตรวจสอบว่าไฟล์ไม่ถูกเปิดค้างใน Excel |
| ตัวเลขวันที่ผิด | ระบบแปลง Excel Serial Date อัตโนมัติ |

## หมายเหตุ

- โปรเจกต์นี้เป็น Web Forms ใหม่ ไม่ได้แก้ไขโปรเจกต์ `camera/` เดิม
- รองรับภาษาไทย (UTF-8) และฟอนต์ IBM Plex Sans Thai
