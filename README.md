# Thai License Plate Detection & OCR

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

ค่าเริ่มต้นคือกล้อง IP `192.168.254.6` (Hikvision RTSP)

```powershell
# วิเคราะห์ครั้งเดียว แล้วพิมพ์ผล หมายเลข / ตัวอักษร / จังหวัด / ความมั่นใจ
dotnet run -- --once

# เปิดหน้าต่างวิดีโอแบบต่อเนื่อง
dotnet run

# ใช้ webcam แทนกล้อง IP
dotnet run -- --webcam
```

ตั้งค่ากล้องด้วย env (แนะนำ) หรือ argument — **อย่า commit รหัสผ่านจริง**

| ตัวแปร / argument | ความหมาย |
|-------------------|----------|
| `CAMERA_URL` / `--url` | RTSP/HTTP เต็มบรรทัด |
| `CAMERA_IP` / `--ip` | ค่าเริ่มต้น `192.168.254.6` |
| `CAMERA_USER` / `--user` | ค่าเริ่มต้น `admin` |
| `CAMERA_PASSWORD` / `--password` | รหัสกล้อง |

ผลลัพธ์ที่อ่านได้จริงเท่านั้นจะถูกรายงาน (ไม่เดาทะเบียน)

- พบรถแต่ป้ายอ่านไม่ชัด: `พบรถ แต่ไม่สามารถอ่านป้ายทะเบียนได้`
- ไม่พบป้ายในภาพ: `ไม่พบรถในภาพ`

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
