# ONNX models

ไฟล์ `.onnx` ไม่ได้อยู่ใน Git เพราะขนาดใหญ่ (~77 MB + ~8 MB)

ดาวน์โหลดอัตโนมัติตอน build บน Windows หรือรันที่ราก repo:

```powershell
.\download-models.ps1
```

| ไฟล์ | ที่มา |
|------|--------|
| `plate_rtdetr.onnx` | https://huggingface.co/Topurrra/rtdetr-license-plate-detection-onnx |
| `th_pp-ocrv5_mobile_rec.onnx` | https://github.com/GreatV/oar-ocr/releases/tag/v0.3.0 |
| `ppocrv5_th_dict.txt` | มีใน repo แล้ว |
