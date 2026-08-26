@echo off
chcp 65001 >nul
set "SRC=%~dp0"
set "DEST=C:\DashboardWebApp"

echo.
echo ========================================
echo  คัดลอกแดชบอร์ดไปที่ไดรฟ์ C:
echo ========================================
echo  ต้นทาง: %SRC%
echo  ปลายทาง: %DEST%
echo.

if not exist "%SRC%DashboardWebApp.sln" (
  echo [ผิดพลาด] ไม่พบ DashboardWebApp.sln
  echo ให้ดับเบิลคลิกไฟล์นี้จากรากของ repo
  echo (โฟลเดอร์ที่มี DashboardWebApp.sln และโฟลเดอร์ DashboardWebApp)
  echo.
  pause
  exit /b 1
)

if not exist "%SRC%DashboardWebApp\index.aspx" (
  echo [ผิดพลาด] ไม่พบโฟลเดอร์ DashboardWebApp\index.aspx
  pause
  exit /b 1
)

mkdir "%DEST%" 2>nul
xcopy /E /I /Y "%SRC%DashboardWebApp" "%DEST%\DashboardWebApp" >nul
copy /Y "%SRC%DashboardWebApp.sln" "%DEST%\" >nul
copy /Y "%SRC%DashboardWebApp\README.md" "%DEST%\" >nul

echo สำเร็จแล้ว
echo.
echo เปิด Visual Studio 2026 แล้วเลือก:
echo   File - Open - Project/Solution
echo   %DEST%\DashboardWebApp.sln
echo.
echo หรือดับเบิลคลิกไฟล์:
echo   %DEST%\DashboardWebApp.sln
echo.
pause
