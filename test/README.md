# DashboardWebApp — ASP.NET Web Forms Dashboard

โปรเจกต์ **ใหม่ทั้งหมด** สำหรับแสดงแดชบอร์ดร้านวัสดุก่อสร้าง โดยอ่านข้อมูลจากไฟล์ Excel `test4.xlsx` ผ่าน **Microsoft ACE OLEDB 12.0**

## โครงสร้างโปรเจกต์ (วางที่ `C:\test`)

```
C:\test\
├── DashboardWebApp.sln
└── DashboardWebApp\
    ├── index.aspx              ← หน้าเริ่มต้น (Default Document)
    ├── index.aspx.cs
    ├── Web.config
    ├── Global.asax
    ├── App_Data\
    │   └── test4.xlsx          ← ฐานข้อมูล Excel
    ├── Content\css\
    │   └── dashboard.css
    ├── Models\
    └── Services\
        └── ExcelDataService.cs ← อ่าน Excel ด้วย ACE OLEDB
```

## โครงสร้างข้อมูลใน test4.xlsx

ไฟล์ Excel มี 4 ชุดข้อมูลบน Sheet1:

| ชุดข้อมูล | คอลัมน์ | จำนวน |
|-----------|---------|-------|
| 🏗️ สินค้า | ProductID, ชื่อสินค้า, ประเภท, ราคาขาย, ต้นทุน, จำนวนคงเหลือ | 10 รายการ |
| 👤 ลูกค้า | CustomerID, ชื่อ, นามสกุล, เบอร์โทร, สมาชิก | 5 รายการ |
| 👷 พนักงาน | EmployeeID, ชื่อ, ตำแหน่ง, เงินเดือน | 4 รายการ |
| 🧾 การขาย | SaleID, วันที่, CustomerID, EmployeeID, ProductID, จำนวน, ราคา, รวมเงิน | 10 รายการ |

## ความต้องการของระบบ

- **Visual Studio 2022/2026** พร้อม workload **ASP.NET and web development**
- **.NET Framework 4.8**
- **IIS Express** (มาพร้อม Visual Studio)
- **Microsoft Access Database Engine 2016 Redistributable** (ACE OLEDB 12.0)
  - ดาวน์โหลด: https://www.microsoft.com/en-us/download/details.aspx?id=54920
  - ติดตั้งเวอร์ชัน **32-bit หรือ 64-bit ให้ตรงกับ Visual Studio / IIS Express**

## วิธีรันด้วย Visual Studio 2026

### ขั้นที่ 1 — เตรียมโฟลเดอร์

1. สร้างโฟลเดอร์ `C:\test` (ถ้ายังไม่มี)
2. คัดลอกโฟลเดอร์ `test` ทั้งหมดจาก repo ไปวางที่ `C:\test`

### ขั้นที่ 2 — เปิดโปรเจกต์

1. เปิด Visual Studio 2026
2. **File → Open → Project/Solution**
3. เลือก `C:\test\DashboardWebApp.sln`

### ขั้นที่ 3 — Build & Run

1. คลิกขวาที่ Solution → **Restore NuGet Packages** (ถ้ามี)
2. กด **Ctrl+Shift+B** เพื่อ Build
3. กด **F5** หรือ **IIS Express** เพื่อรัน
4. เบราว์เซอร์จะเปิด `index.aspx` อัตโนมัติ (ตั้งเป็น Default Document ใน Web.config)

### URL ตัวอย่าง

```
http://localhost:50400/index.aspx
```

## การทำงานของระบบอ่าน Excel

`ExcelDataService.cs` ใช้ connection string:

```
Provider=Microsoft.ACE.OLEDB.12.0;
Data Source=[App_Data\test4.xlsx];
Extended Properties='Excel 12.0 Xml;HDR=NO;IMEX=1';
```

จากนั้นอ่าน `[Sheet1$]` แล้วแยกข้อมูลตาม prefix ของรหัส:

- `P` = สินค้า
- `C` = ลูกค้า
- `E` = พนักงาน
- `S` = การขาย

## แดชบอร์ดแสดงอะไรบ้าง

- การ์ดสรุป: ยอดขายรวม, จำนวนสินค้า, ลูกค้า/สมาชิก, พนักงาน, รายการขาย, มูลค่าสต็อก
- ตารางรายการขายล่าสุด
- กราฟแท่งยอดขายตามประเภทสินค้า
- ตารางสินค้าคงเหลือ
- ตารางลูกค้าและพนักงาน

## แก้ปัญหาเบื้องต้น

| ปัญหา | วิธีแก้ |
|-------|---------|
| `Microsoft.ACE.OLEDB.12.0 provider is not registered` | ติดตั้ง Access Database Engine ให้ตรง bitness กับ IIS Express |
| ไม่พบไฟล์ Excel | ตรวจสอบว่า `App_Data\test4.xlsx` มีอยู่ |
| ภาษาไทยเพี้ยน | ตรวจสอบ Web.config มี `globalization` encoding utf-8 |
