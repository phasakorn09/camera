# Downloads ONNX models required by camera/ (gitignored because of size).
$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$modelsDir = Join-Path $PSScriptRoot "camera\Models"
New-Item -ItemType Directory -Force -Path $modelsDir | Out-Null

$files = @(
    @{
        Name = "plate_rtdetr.onnx"
        Url  = "https://huggingface.co/Topurrra/rtdetr-license-plate-detection-onnx/resolve/main/plate_rtdetr.onnx"
    },
    @{
        Name = "th_pp-ocrv5_mobile_rec.onnx"
        Url  = "https://github.com/GreatV/oar-ocr/releases/download/v0.3.0/th_pp-ocrv5_mobile_rec.onnx"
    }
)

foreach ($file in $files) {
    $dest = Join-Path $modelsDir $file.Name
    if ((Test-Path $dest) -and ((Get-Item $dest).Length -gt 1MB)) {
        Write-Host "Already present: $($file.Name)"
        continue
    }

    Write-Host "Downloading $($file.Name) ..."
    Invoke-WebRequest -Uri $file.Url -OutFile $dest -UseBasicParsing
    Write-Host ("Saved {0} ({1:N1} MB)" -f $file.Name, ((Get-Item $dest).Length / 1MB))
}

Write-Host "Models ready in $modelsDir"
