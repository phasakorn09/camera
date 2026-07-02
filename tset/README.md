# tset — แดชบอร์ด ASP.NET Web Forms (Excel Data Source)

โปรเจกต์ **ใหม่ทั้งหมด** สำหรับแสดงแดชบอร์ดร้านวัสดุก่อสร้าง โดยอ่านข้อมูลจากไฟล์ Excel `test4.xlsx` ด้วย **Microsoft ACE OLEDB 12.0**

> **Path โปรเจกต์:** `C:\test\tset\`

---

## ขั้นที่ 1 — ความต้องการและโครงสร้างข้อมูล Excel

### ไฟล์ต้นทาง
- `App_Data/test4.xlsx` (Sheet1)

### โครงสร้างข้อมูลใน Excel (4 ตารางใน Sheet เดียว)

| ตาราง | Range ใน Excel | คอลัมน์ |
|-------|----------------|---------|
| 🏗️ สินค้า | A2:F12 | ProductID, ชื่อสินค้า, ประเภท, ราคาขาย, ต้นทุน, จำนวนคงเหลือ |
| 👤 ลูกค้า | I2:M7 | CustomerID, ชื่อ, นามสกุล, เบอร์โทร, สมาชิก |
| 👷 พนักงาน | H10:L14 | EmployeeID, ชื่อ, ตำแหน่ง, เงินเดือน |
| 🧾 การขาย | A17:H27 | SaleID, วันที่, CustomerID, EmployeeID, ProductID, จำนวน, ราคาต่อหน่วย, รวมเงิน |

### สรุปข้อมูลตัวอย่าง
- สินค้า 10 รายการ (ปูน, อิฐ, เหล็ก, ทราย, หิน, ท่อ, สี, ฮาร์ดแวร์, เคมีภัณฑ์)
- ลูกค้า 5 คน (4 สมาชิก)
- พนักงาน 4 คน
- การขาย 10 รายการ (ม.ค.–พ.ค. 2026)

---

## ขั้นที่ 2 — โครงสร้างโปรเจกต์

```
C:\test\tset\
├── tset.sln                          ← Solution file
└── tset\
    ├── tset.csproj                   ← Project file (.NET Framework 4.8)
    ├── Web.config
    ├── Global.asax / Global.asax.cs
    ├── index.aspx                    ← หน้าแดชบอร์ดหลัก
    ├── index.aspx.cs
    ├── index.aspx.designer.cs
    ├── App_Data\
    │   └── test4.xlsx                ← ไฟล์ Excel ฐานข้อมูล
    ├── App_Code\
    │   ├── Models\                   ← Product, Customer, Employee, Sale
    │   └── Services\
    │       └── ExcelDataService.cs   ← อ่าน Excel ด้วย ACE OLEDB
    └── Content\
        └── css\
            └── dashboard.css         ← สไตล์แดชบอร์ด
```

---

## ขั้นที่ 3 — ระบบอ่านข้อมูล Excel (ACE OLEDB 12.0)

### Connection String
```csharp
Provider=Microsoft.ACE.OLEDB.12.0;
Data Source=C:\test\tset\tset\App_Data\test4.xlsx;
Extended Properties='Excel 12.0 Xml;HDR=YES;IMEX=1';
```

### วิธีอ่าน (Range Query)
```csharp
// อ่านสินค้า
SELECT * FROM [Sheet1$A2:F12]

// อ่านลูกค้า
SELECT * FROM [Sheet1$I2:M7]

// อ่านพนักงาน
SELECT * FROM [Sheet1$H10:L14]

// อ่านการขาย
SELECT * FROM [Sheet1$A17:H27]
```

### ข้อกำหนดเบื้องต้น (Prerequisites)
ต้องติดตั้ง **Microsoft Access Database Engine 2016 Redistributable** ก่อนรัน:

1. ดาวน์โหลด: https://www.microsoft.com/en-us/download/details.aspx?id=54920
2. เลือก **AccessDatabaseEngine.exe** (x86) หรือ **AccessDatabaseEngine_X64.exe** (x64)
3. **สำคัญ:** เลือกเวอร์ชันให้ตรงกับ Visual Studio (VS 2026 ใช้ x64 → ติดตั้ง X64)

---

## ขั้นที่ 4 — หน้าแดชบอร์ด (index.aspx)

### ฟีเจอร์ UI
- **Summary Cards** — ยอดขายรวม, จำนวนสินค้า, ลูกค้า, พนักงาน
- **Bar Chart** — ยอดขายรายเดือน (Chart.js)
- **Doughnut Chart** — ยอดขายตามประเภทสินค้า
- **ตารางข้อมูล** — การขาย, สินค้า, ลูกค้า, พนักงาน
- **Dark Theme** — ดีไซน์ทันสมัย รองรับภาษาไทย

---

## ขั้นที่ 5 — วิธีรันด้วย Visual Studio 2026

### 1. สร้างโฟลเดอร์และคัดลอกโปรเจกต์
```powershell
mkdir C:\test\tset
# คัดลอกไฟล์ทั้งหมดจาก repo ไปที่ C:\test\tset\
```

### 2. เปิด Solution
1. เปิด **Visual Studio 2026**
2. เลือก **File → Open → Project/Solution**
3. เลือกไฟล์ `C:\test\tset\tset.sln`

### 3. ตั้งค่า Platform (ถ้าจำเป็น)
1. **Build → Configuration Manager**
2. ตรวจสอบ Platform เป็น **Any CPU**
3. ถ้า ACE OLEDB ไม่ทำงาน ลองเปลี่ยนเป็น **x64** ให้ตรงกับ ACE ที่ติดตั้ง

### 4. ตั้งค่า Startup Page
1. คลิกขวาที่ `index.aspx` → **Set As Start Page**

### 5. รันโปรเจกต์
1. กด **F5** หรือ **Ctrl+F5** (Run Without Debugging)
2. IIS Express จะเปิดเบราว์เซอร์ที่ `http://localhost:50400/index.aspx`

### 6. แก้ไขปัญหาที่พบบ่อย

| ปัญหา | วิธีแก้ |
|-------|---------|
| `The 'Microsoft.ACE.OLEDB.12.0' provider is not registered` | ติดตั้ง Access Database Engine (ดูขั้นที่ 3) |
| Platform mismatch (x86 vs x64) | ตั้ง IIS Express เป็น x64 ใน Project Properties → Web |
| ไม่พบไฟล์ Excel | ตรวจสอบว่า `App_Data\test4.xlsx` มีอยู่ |
| ตัวอักษรไทยเพี้ยน | ตรวจสอบ `<globalization>` ใน Web.config |

---

## เทคโนโลยีที่ใช้

| รายการ | รายละเอียด |
|--------|-----------|
| Framework | ASP.NET Web Forms (.NET Framework 4.8) |
| IDE | Visual Studio 2026 |
| ฐานข้อมูล | ไฟล์ Excel (.xlsx) |
| OLEDB Provider | Microsoft ACE OLEDB 12.0 |
| Chart | Chart.js 4.4.1 (CDN) |
| หน้าเริ่มต้น | index.aspx |

---

## หมายเหตุ

- โปรเจกต์นี้เป็นโปรเจกต์ **ใหม่ทั้งหมด** ไม่เกี่ยวข้องกับโปรเจกต์อื่นใน workspace
- ไฟล์ Excel อยู่ที่ `App_Data/test4.xlsx` — สามารถแก้ไขข้อมูลใน Excel แล้ว Refresh หน้าเว็บได้ทันที
