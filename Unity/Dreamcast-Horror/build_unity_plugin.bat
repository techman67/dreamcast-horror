@echo off
REM =====================================================================
REM Builds dreamcast_horror.dll as 64-bit (x86_64) for the Unity Editor.
REM =====================================================================

setlocal enabledelayedexpansion

pushd "%~dp0..\.." >nul
set "REPO=%CD%"
popd >nul

set "OUT=%~dp0Assets\Plugins\x86_64"

set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"

if not exist "%VSWHERE%" (
    echo ERROR: vswhere.exe not found.
    if not "%~1"=="--non-interactive" pause
    exit /b 1
)

set "VSPATH="
for /f "usebackq tokens=*" %%i in (`"%VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do set "VSPATH=%%i"

if "%VSPATH%"=="" (
    echo ERROR: No Visual Studio with C++ x64 tools found.
    if not "%~1"=="--non-interactive" pause
    exit /b 1
)

call "%VSPATH%\VC\Auxiliary\Build\vcvarsall.bat" x64
if errorlevel 1 (
    echo ERROR: vcvarsall.bat x64 failed.
    if not "%~1"=="--non-interactive" pause
    exit /b 1
)

if not exist "%OUT%" mkdir "%OUT%"
set "BUILD=%TEMP%\dreamcast-horror-build-%RANDOM%-%RANDOM%"
mkdir "%BUILD%"
if errorlevel 1 exit /b 1
pushd "%BUILD%"

echo.
echo ============================================================
echo Building dreamcast_horror.dll (x64)...
echo ============================================================
echo.

cl /nologo /std:c++17 /O2 /EHsc /LD "%REPO%\UnityBridge.cpp" "%REPO%\Game\Core\RoomData.cpp" "%REPO%\Game\Core\AudioData.cpp" "%REPO%\Game\Core\SaveData.cpp" "%REPO%\Game\Core\WorldData.cpp" "%REPO%\Game\Compatibility\World.cpp" "%REPO%\Game\Compatibility\Game.cpp" "%REPO%\Game\Compatibility\StaticCollisionBackend.cpp" "%REPO%\Game\Compatibility\CharacterCollision.cpp" "%REPO%\Game\Gameplay\Player.cpp" "%REPO%\Game\Gameplay\KeyDoor.cpp" /I "%REPO%\Game\Compatibility" /I "%REPO%\Game\Core" /I "%REPO%\Game\Gameplay" /Fe:"%OUT%\dreamcast_horror.dll" /link /IMPLIB:"%BUILD%\dreamcast_horror.lib"

set "BUILD_RESULT=%ERRORLEVEL%"
popd
if not "%BUILD_RESULT%"=="0" (
    echo.
    echo ============================================================
    echo BUILD FAILED.
    echo ============================================================
    if not "%~1"=="--non-interactive" pause
    exit /b 1
)

echo.
echo ============================================================
echo BUILD SUCCEEDED:
echo %OUT%\dreamcast_horror.dll
echo ============================================================
echo.
echo Return to Unity; it will reimport the plugin automatically.
echo.

if not "%~1"=="--non-interactive" pause
endlocal