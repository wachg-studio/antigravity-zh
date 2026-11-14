@echo off
chcp 65001 >nul
cd /d "%~dp0"

if not exist "AntigravityZh.exe" (
    echo 找不到 AntigravityZh.exe。
    pause
    exit /b 1
)

"AntigravityZh.exe" uninstall
echo.
pause
