@echo off
chcp 65001 >nul
set REPO_ROOT=C:\Users\Wiwat.L\source\repos\ExcelDashboard
set SLN=%REPO_ROOT%\ExcelDashboard\ExcelDashboard.sln

echo ============================================
echo  ExcelDashboard - Setup Script
echo  Target: %REPO_ROOT%
echo ============================================
echo.

if not exist "C:\Users\Wiwat.L\source\repos" (
    mkdir "C:\Users\Wiwat.L\source\repos"
)

if not exist "%REPO_ROOT%\.git" (
    echo [1/2] Cloning repository...
    git clone -b cursor/aspnet-excel-dashboard-7d51 https://github.com/phasakorn09/camera.git "%REPO_ROOT%"
) else (
    echo [1/2] Repository exists, updating branch...
    cd /d "%REPO_ROOT%"
    git fetch origin
    git checkout cursor/aspnet-excel-dashboard-7d51
    git pull origin cursor/aspnet-excel-dashboard-7d51
)

echo.
echo [2/2] Done!
echo.
echo Solution file:
echo   %SLN%
echo.
echo Open in Visual Studio 2026, then press F5.
echo URL: http://localhost:51235/index.aspx
echo.
pause
