@echo off
chcp 65001 >nul
title Thanh Van Ky - khoi dong Unity bang Direct3D 11
rem ===================================================================
rem  Fix crash Unity Editor (D3D12SwapChain::Present -> nvwgf2umx.dll)
rem  Chay file nay de mo project bang Direct3D 11 thay vi Direct3D 12.
rem  Sua dong UNITY= ben duoi neu ban cai Unity o cho khac.
rem ===================================================================

set "UNITY=C:\Users\USER\Downloads\UnityEditor\6000.6.1f1\Editor\Unity.exe"
set "PROJECT=C:\Users\USER\Downloads\ThienKhuyet"
rem  Duong dan tuyet doi toi thu muc project (sua dong nay neu ban de project o cho khac)

if not exist "%UNITY%" (
    echo [LOI] Khong tim thay Unity.exe tai:
    echo        %UNITY%
    echo        Hay mo file .bat nay bang Notepad va sua dong "set UNITY=" cho dung.
    pause
    exit /b 1
)
if not exist "%PROJECT%" (
    echo [LOI] Khong tim thay thu muc project:
    echo        %PROJECT%
    pause
    exit /b 1
)

echo Dang mo Unity voi Direct3D 11...
echo   Editor : %UNITY%
echo   Project: %PROJECT%
echo.
"%UNITY%" -force-d3d11 -projectPath "%PROJECT%"

if errorlevel 1 (
    echo.
    echo Unity da thoat voi ma loi %errorlevel%.
    pause
)
