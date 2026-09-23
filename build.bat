@echo off
echo ========================================
echo   ADOFAI Agent Key Viewer - Build Script
echo ========================================
echo.

REM Set ADOFAI game path (modify as needed)
set ADOFAI_PATH=D:\Steam\steamapps\common\A Dance of Fire and Ice

REM Check if game path exists
if not exist "%ADOFAI_PATH%" (
    echo [WARNING] ADOFAI game directory not found: %ADOFAI_PATH%
    echo Please modify ADOFAI_PATH variable in build.bat
    echo.
    pause
    exit /b 1
)

echo [INFO] ADOFAI path: %ADOFAI_PATH%
echo.

REM Build project. The core project references the bootstrap project,
REM so building it also builds ADOFAI.AgentKeyViewer.dll.
echo [BUILD] Building project...
dotnet build AgentKeyViewer.Core.csproj -c Release /p:ADOFAIPath="%ADOFAI_PATH%"

if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Build failed!
    pause
    exit /b 1
)

echo.
echo [SUCCESS] Build completed!
echo.

REM Copy files to Mods directory
set MOD_DIR=%ADOFAI_PATH%\Mods\ADOFAI.AgentKeyViewer
echo [DEPLOY] Deploying to: %MOD_DIR%

if not exist "%MOD_DIR%" mkdir "%MOD_DIR%"

REM Bootstrap (UMM entry, stable) + Core (hot-swappable)
copy /Y "bootstrap\bin\Release\net48\ADOFAI.AgentKeyViewer.dll" "%MOD_DIR%\"
copy /Y "bin\Release\net48\AgentKeyViewer.Core.dll" "%MOD_DIR%\"
copy /Y "Info.json" "%MOD_DIR%\"
copy /Y "Repository.json" "%MOD_DIR%"

echo.
echo [SUCCESS] Mod deployed to game directory!
echo.
echo Press any key to exit...
pause >nul
