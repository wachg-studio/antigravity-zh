@echo off
chcp 65001 >nul
cd /d "%~dp0"

if not exist "AntigravityZh.exe" (
    echo 找不到 AntigravityZh.exe,请确认压缩包已完整解压。
    pause
    exit /b 1
)

"AntigravityZh.exe" install
echo.
pause
