@echo off
REM Publish script for the MCP Bridge (Windows)
REM Creates self-contained executables for all supported platforms

setlocal enabledelayedexpansion

set "PROJECT_DIR=%~dp0"
set "OUTPUT_DIR=%PROJECT_DIR%publish"

echo Publishing MCP Bridge for all platforms...
echo Output directory: %OUTPUT_DIR%

REM Clean previous builds
if exist "%OUTPUT_DIR%" rmdir /s /q "%OUTPUT_DIR%"
mkdir "%OUTPUT_DIR%"

REM Define platforms
set PLATFORMS=win-x64 win-arm64 linux-x64 linux-arm64 osx-x64 osx-arm64

for %%P in (%PLATFORMS%) do (
    echo.
    echo Publishing for %%P...
    
    dotnet publish "%PROJECT_DIR%HeadlessIdeMcp.Bridge.csproj" ^
        -c Release ^
        -r %%P ^
        --self-contained ^
        -p:PublishSingleFile=true ^
        -p:PublishTrimmed=false ^
        -p:IncludeNativeLibrariesForSelfExtract=true ^
        -o "%OUTPUT_DIR%\%%P"
    
    if !errorlevel! neq 0 (
        echo Error publishing for %%P
        exit /b !errorlevel!
    )
    
    echo OK Published to %OUTPUT_DIR%\%%P
)

echo.
echo OK All platforms published successfully!
echo.
echo Executables created in: %OUTPUT_DIR%
dir /b "%OUTPUT_DIR%"

endlocal
