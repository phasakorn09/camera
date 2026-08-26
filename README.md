# Thai License Plate Detection & OCR

## หาโฟลเดอร์แดชบอร์ดไม่เจอ?

โปรเจกต์ ASP.NET Web Forms **ไม่ได้อยู่ใน `camera/` และไม่ได้อยู่ใน `test/` แล้ว**

เปิดไฟล์เหล่านี้ที่ **รากของ repo**:

| ไฟล์ / โฟลเดอร์ | ใช้ทำอะไร |
|-----------------|-----------|
| `หาแดชบอร์ดที่นี่.txt` | อธิบาย path |
| `DashboardWebApp.sln` | เปิดใน Visual Studio 2026 |
| `DashboardWebApp\` | โค้ดหน้า `index.aspx` และไฟล์ Excel |
| `คัดลอกไป-C-DashboardWebApp.bat` | สร้างโฟลเดอร์ `C:\DashboardWebApp` ให้ |

ยังไม่มีโฟลเดอร์ `C:\DashboardWebApp` จนกว่าจะรันไฟล์ `.bat` หรือคัดลอกเอง

รายละเอียดเต็ม: [DashboardWebApp/README.md](DashboardWebApp/README.md)

---

แอป C# (.NET 10) สำหรับตรวจจับป้ายทะเบียนไทยจาก webcam หรือ RTSP แล้วอ่านตัวอักษรด้วย PaddleOCR

## ความต้องการของระบบ

- Windows 10/11
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- กล้อง USB หรือสตรีม RTSP
- ไฟล์โมเดล ONNX (ดาวน์โหลดแยก — ไม่ได้อยู่ใน repo)

## โครงสร้างโปรเจกต์

```
camera/
├── camera/              # โปรเจกต์หลัก
│   ├── Models/          # วางไฟล์โมเดลที่นี่
│   └── *.cs
└── camera.slnx
```

## ติดตั้งโมเดล

วางไฟล์ต่อไปนี้ใน `camera/Models/` (มี `Copy to Output Directory` ใน `.csproj` แล้ว):

| ไฟล์ | หน้าที่ |
|------|--------|
| `plate_rtdetr.onnx` | ตรวจจับตำแหน่งป้าย (RT-DETR) |
| `th_pp-ocrv5_mobile_rec.onnx` | อ่านตัวอักษรไทย (PaddleOCR PP-OCRv5) |
| `ppocrv5_th_dict.txt` | พจนานุกรมตัวอักษร (มีใน repo) |

> ไฟล์ `.onnx` ถูก ignore ใน Git เพราะขนาดใหญ่ (~77 MB + ~8 MB)

## รันโปรแกรม

```powershell
cd camera
dotnet build
dotnet run
```

### แหล่งวิดีโอ

แก้ใน `Program.cs`:

- **Webcam (ค่าเริ่มต้น):** `new VideoCapture(0)`
- **RTSP:** uncomment บรรทัด RTSP และ comment webcam — **อย่า commit URL/รหัสผ่านจริง**

## การใช้งาน

| ปุ่ม | การทำงาน |
|------|----------|
| ESC | ออกจากโปรแกรม |
| `[` | ลดความคมชัด |
| `]` | เพิ่มความคมชัด |

Pipeline: จับเฟรม → RT-DETR หาป้าย → ตัด/ปรับภาพ → PaddleOCR → จับคู่จังหวัด → โหวตหลายเฟรม

## เทคโนโลยี

- [OpenCvSharp4](https://github.com/shimat/opencvsharp) — วิดีโอและประมวลผลภาพ
- [ONNX Runtime](https://onnxruntime.ai/) — inference
- RT-DETR + PaddleOCR Thai PP-OCRv5
