@echo off
REM Build Script for Stat Editor Mod
REM This script builds the project and packages the DLL into a zip file

set PROJECT_NAME=StatEditorMod
set VERSION=2.0.0
set RELEASE_DIR=release
set ZIP_FILE=%PROJECT_NAME%-v%VERSION%.zip

echo Building %PROJECT_NAME% v%VERSION%...
echo.

REM Clean previous build
echo Cleaning previous build...
if exist bin rmdir /s /q bin
if exist obj rmdir /s /q obj
if exist %RELEASE_DIR% rmdir /s /q %RELEASE_DIR%

REM Build the project
echo Building project...
dotnet build --configuration Release
if %ERRORLEVEL% neq 0 (
    echo Build failed!
    exit /b 1
)

REM Create release directory
echo Creating release directory...
mkdir %RELEASE_DIR%

REM Copy DLL
echo Copying DLL...
copy "bin\Release\net472\%PROJECT_NAME%.dll" "%RELEASE_DIR%\%PROJECT_NAME%.dll"

REM Create installation instructions
echo Creating installation instructions...
(
echo %PROJECT_NAME% v%VERSION%
echo ==================
echo.
echo Installation Instructions:
echo.
echo 1. Copy %PROJECT_NAME%.dll to your Sun Haven BepInEx plugins folder:
echo    Sun Haven\BepInEx\plugins\%PROJECT_NAME%.dll
echo.
echo 2. Launch the game - the mod will load automatically
echo.
echo 3. Look for the ⚔️ icon in the top-left corner of the screen
echo    Click it to open the Stat Editor GUI
echo.
echo 4. Adjust sliders for combat stats and enter gold amount
echo    Click "Apply Changes" to save modifications
echo.
echo 5. Changes are permanent and will save to your save file
echo.
echo Uninstallation:
echo Simply delete %PROJECT_NAME%.dll from the BepInEx\plugins\ folder
echo.
echo For more information, visit:
echo https://github.com/AliesterEroan/SunHavenStatEditorMod
echo.
echo Built by Aliester Eroan
) > "%RELEASE_DIR%\INSTALL.txt"

REM Copy README
echo Copying README...
copy README.md "%RELEASE_DIR%\README.md"

REM Create zip file (requires PowerShell)
echo Creating zip file...
powershell -Command "Compress-Archive -Path '%RELEASE_DIR%\*' -DestinationPath '%ZIP_FILE%' -Force"

echo.
echo Build complete!
echo Release package: %ZIP_FILE%
echo.
pause
