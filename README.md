# Thai License Plate Detection & OCR

แอป C# (.NET 10) สำหรับตรวจจับป้ายทะเบียนไทยจาก webcam หรือ RTSP แล้วอ่านตัวอักษรด้วย PaddleOCR

## สิ่งที่มี / สิ่งที่ขาด

| | สถานะ |
|--|--------|
| ซอร์สโค้ดอ่านป้ายไทย (ตรวจจับ + OCR + จังหวัด) | มีใน repo |
| `camera.sln` / `camera.slnx` สำหรับ Visual Studio | มีใน repo |
| พจนานุกรม `ppocrv5_th_dict.txt` | มีใน repo |
| สคริปต์ดาวน์โหลดโมเดล `download-models.ps1` | มีใน repo — รันเองหรือดาวน์โหลดตอน build บน Windows |
| โมเดล `plate_rtdetr.onnx` (~77 MB) | ไม่ได้อยู่ใน Git — ดาวน์โหลดด้วยสคริปต์ |
| โมเดล `th_pp-ocrv5_mobile_rec.onnx` (~8 MB) | ไม่ได้อยู่ใน Git — ดาวน์โหลดด้วยสคริปต์ |
| Windows + Visual Studio / .NET 10 SDK | ต้องมีบนเครื่องที่รัน |
| กล้อง IP `192.168.254.6` ในวง LAN เดียวกับเครื่อง | ต้องมีตอนรันจริง |

## ความต้องการของระบบ

- Windows 10/11
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- กล้อง USB หรือสตรีม RTSP
- ไฟล์โมเดล ONNX (ดาวน์โหลดแยก — ไม่ได้อยู่ใน repo)

## โครงสร้างโปรเจกต์

```
camera.sln / camera.slnx
camera/
├── camera.csproj
├── Models/              # dict ใน git — ไฟล์ .onnx ดาวน์โหลดด้วยสคริปต์
└── *.cs
```

## ติดตั้งโมเดล

บน Windows โมเดลจะถูกดาวน์โหลดอัตโนมัติตอน build ครั้งแรก หรือรันที่ราก repo:

```powershell
.\download-models.ps1
```

ไฟล์จะไปอยู่ที่ `camera/Models/` (มี `Copy to Output Directory` ใน `.csproj` แล้ว):

| ไฟล์ | หน้าที่ |
|------|--------|
| `plate_rtdetr.onnx` | ตรวจจับตำแหน่งป้าย (RT-DETR) |
| `th_pp-ocrv5_mobile_rec.onnx` | อ่านตัวอักษรไทย (PaddleOCR PP-OCRv5) |
| `ppocrv5_th_dict.txt` | พจนานุกรมตัวอักษร (มีใน repo) |

> ไฟล์ `.onnx` ถูก ignore ใน Git เพราะขนาดใหญ่ (~77 MB + ~8 MB)

## รันโปรแกรม

เปิด **`camera.sln`** ใน Visual Studio (หรือ `camera.slnx`) เลือกโปรไฟล์ Debug แล้วกด F5

โปรไฟล์ใน Visual Studio:

- **Live (IP camera)** — เปิดหน้าต่างกล้องต่อเนื่อง
- **Once** — อ่านครั้งเดียวแล้วพิมพ์ผล
- **Webcam** — ใช้กล้อง USB

หรือจากเทอร์มินัล:

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
