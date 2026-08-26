# DashboardWebApp — แดชบอร์ด ASP.NET Web Forms

โปรเจกต์ **ASP.NET Web Forms** สำหรับแสดงแดชบอร์ดร้านวัสดุก่อสร้าง โดยอ่านไฟล์ Excel `.xlsx` ด้วย **Office Open XML** (ไม่ต้องติดตั้ง Microsoft ACE OLEDB)

## ขั้นตอนที่ 1 — กำหนดความต้องการ

| รายการ | ค่า |
|--------|------|
| เป้าหมาย | สร้างแดชบอร์ดร้านวัสดุก่อสร้าง |
| ภาษา / เฟรมเวิร์ก | ASP.NET Web Forms (.aspx) + C# |
| หน้าเริ่มต้น | `index.aspx` |
| IDE | Visual Studio 2026 |
| Path ที่แนะนำบนเครื่อง Windows | `C:\DashboardWebApp` |
| ข้อมูล | ไฟล์ Excel `.xlsx` |
| Target framework | .NET Framework 4.8 |

### ฟังก์ชันหลัก

- อ่านไฟล์ `.xlsx` จาก `App_Data\test4.xlsx`
- อัปโหลดไฟล์ `.xlsx` ของผู้ใช้แล้วรีเฟรชแดชบอร์ด
- แสดง KPI, กราฟ, ตารางขาย, สินค้าคงเหลือ, ลูกค้า และพนักงาน
- รองรับภาษาไทย (UTF-8)

## ขั้นตอนที่ 2 — โครงสร้างโปรเจกต์

วางโปรเจกต์ที่ `C:\DashboardWebApp` ดังนี้:

```
C:\DashboardWebApp\
├── DashboardWebApp.sln
└── DashboardWebApp\
    ├── index.aspx                 ← หน้าเริ่มต้น (Default Document)
    ├── index.aspx.cs
    ├── Web.config
    ├── Global.asax
    ├── App_Data\
    │   └── test4.xlsx             ← ข้อมูลตัวอย่าง
    ├── Content\
    │   ├── css\dashboard.css
    │   └── js\dashboard.js
    ├── Models\
    └── Services\
        ├── OpenXmlExcelReader.cs  ← อ่าน .xlsx (ZIP + XML)
        └── ExcelDataService.cs    ← แปลงชีตเป็นโมเดลแดชบอร์ด
```

ใน Git repo โฟลเดอร์นี้อยู่ที่ `test/` — คัดลอกทั้งโฟลเดอร์ `test` ไปที่ `C:\DashboardWebApp`

## ขั้นตอนที่ 3 — การอ่านไฟล์ Excel (.xlsx)

`OpenXmlExcelReader` เปิดไฟล์ `.xlsx` ซึ่งเป็น ZIP ของ XML ตามมาตรฐาน Office Open XML:

1. อ่าน `xl/workbook.xml` เพื่อหาชื่อชีต
2. อ่าน `xl/sharedStrings.xml` สำหรับข้อความ
3. อ่าน `xl/worksheets/sheetN.xml` เป็นแถว/คอลัมน์
4. แปลงวันที่จาก serial date ของ Excel เมื่อเซลล์ใช้รูปแบบวันที่

`ExcelDataService` รองรับชีตชื่อ:

| ชีต | หัวตาราง |
|-----|----------|
| สินค้า / Products | ProductID, ชื่อสินค้า, ประเภท, ราคาขาย, ต้นทุน, จำนวนคงเหลือ |
| ลูกค้า / Customers | CustomerID, ชื่อ, นามสกุล, เบอร์โทร, สมาชิก |
| พนักงาน / Employees | EmployeeID, ชื่อ, ตำแหน่ง, เงินเดือน |
| การขาย / Sales | SaleID, วันที่, CustomerID, EmployeeID, ProductID, จำนวน, ราคา, รวมเงิน |

ถ้าไม่มีชีตตามชื่อ จะลองอ่านชีตแรกแบบรวมตาราง (รหัสขึ้นต้นด้วย `P` / `C` / `E` / `S`)

ไฟล์ตัวอย่าง `test4.xlsx` มีข้อมูล:

- สินค้า 10 รายการ
- ลูกค้า 5 รายการ
- พนักงาน 4 คน
- การขาย 12 รายการ

สร้างไฟล์ตัวอย่างใหม่ได้ด้วย:

```bash
python3 test/scripts/generate_excel.py
```

## ขั้นตอนที่ 4 — หน้า UX / UI

หน้า `index.aspx` ออกแบบเป็นแดชบอร์ดโทน navy + amber (ธีมร้านวัสดุก่อสร้าง):

- แถบด้านข้าง BuildBoard และลิงก์ไปยังแต่ละส่วน
- การ์ด KPI 6 ใบ (ยอดขาย, สินค้า, ลูกค้า/สมาชิก, พนักงาน, รายการขาย, มูลค่าสต็อก)
- กราฟแท่งยอดขายตามประเภท และกราฟโดนัทสัดส่วนสต็อก (Chart.js)
- ตารางขายล่าสุด, สินค้าใกล้หมด, สินค้าคงเหลือ, ลูกค้า, พนักงาน
- ปุ่มอัปโหลด `.xlsx`, ใช้ไฟล์ตัวอย่าง, ดาวน์โหลดเทมเพลต
- รองรับหน้าจอแคบ (responsive)

ไฟล์ `preview.html` คือตัวอย่าง UI แบบ static จากข้อมูล `test4.xlsx` สำหรับดูเลย์เอาต์โดยไม่ต้องรัน IIS

## ขั้นตอนที่ 5 — วิธีรันด้วย Visual Studio 2026

### ความต้องการของเครื่อง

- Windows 10/11
- Visual Studio 2026 ติดตั้ง workload **ASP.NET and web development**
- .NET Framework 4.8 Developer Pack (มากับ Visual Studio)
- **ไม่ต้อง** ติดตั้ง Microsoft Access Database Engine

### ขั้นที่ 1 — วางโปรเจกต์ที่ไดรฟ์ C:

1. สร้างโฟลเดอร์ `C:\DashboardWebApp`
2. คัดลอกเนื้อหาใน `test\` จาก repo ไปไว้ที่ `C:\DashboardWebApp`
3. ตรวจว่ามีไฟล์ `C:\DashboardWebApp\DashboardWebApp.sln` และ `C:\DashboardWebApp\DashboardWebApp\App_Data\test4.xlsx`

### ขั้นที่ 2 — เปิดโปรเจกต์

1. เปิด **Visual Studio 2026**
2. เลือก **File → Open → Project/Solution**
3. เปิด `C:\DashboardWebApp\DashboardWebApp.sln`
4. คลิกขวาที่โปรเจกต์ `DashboardWebApp` → **Set as Startup Project**
5. คลิกขวา `index.aspx` → **Set As Start Page** (ถ้ายังไม่ได้ตั้ง)

### ขั้นที่ 3 — Build และรัน

1. กด **Ctrl+Shift+B** เพื่อ Build
2. กด **F5** หรือปุ่ม **IIS Express**
3. เบราว์เซอร์จะเปิดหน้าเริ่มต้น `index.aspx` อัตโนมัติ

URL ตัวอย่าง:

```
http://localhost:50400/index.aspx
http://localhost:50400/
```

### ขั้นที่ 4 — ทดลองอัปโหลด Excel

1. กด **ดาวน์โหลดตัวอย่าง** เพื่อได้ไฟล์ `test4.xlsx`
2. แก้ข้อมูลใน Excel แล้วบันทึกเป็น `.xlsx`
3. กด **เลือกไฟล์ .xlsx** → **โหลดข้อมูล**
4. กด **ใช้ไฟล์ตัวอย่าง** เพื่อกลับไปใช้ `App_Data\test4.xlsx`

## แก้ปัญหาเบื้องต้น

| ปัญหา | วิธีแก้ |
|-------|---------|
| ไม่พบไฟล์ Excel | ตรวจว่ามี `App_Data\test4.xlsx` |
| อัปโหลดไม่ได้ | ใช้เฉพาะ `.xlsx` ขนาดไม่เกิน 10 MB |
| ภาษาไทยเพี้ยน | `Web.config` ต้องมี `globalization` encoding utf-8 |
| เปิดใน Visual Studio แล้วไม่มีเทมเพลต Web Forms | ติดตั้ง workload ASP.NET and web development |
| พอร์ตถูกใช้แล้ว | ในโปรเจกต์ Properties → Web เปลี่ยน IIS Express URL |
