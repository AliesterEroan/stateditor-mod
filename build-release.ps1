# Build Script for Stat Editor Mod
# This script builds the project and packages the DLL into a zip file

$ErrorActionPreference = "Stop"

# Configuration
$ProjectName = "StatEditorMod"
$Version = "2.0.0"
$ReleaseDir = "release"
$ZipFileName = "$ProjectName-v$Version.zip"

Write-Host "Building $ProjectName v$Version..." -ForegroundColor Cyan

# Clean previous build
Write-Host "Cleaning previous build..." -ForegroundColor Yellow
if (Test-Path "bin") {
    Remove-Item -Recurse -Force "bin"
}
if (Test-Path "obj") {
    Remove-Item -Recurse -Force "obj"
}
if (Test-Path $ReleaseDir) {
    Remove-Item -Recurse -Force $ReleaseDir
}

# Build the project
Write-Host "Building project..." -ForegroundColor Yellow
dotnet build --configuration Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Create release directory
Write-Host "Creating release directory..." -ForegroundColor Yellow
New-Item -ItemType Directory -Path $ReleaseDir | Out-Null

# Copy DLL
Write-Host "Copying DLL..." -ForegroundColor Yellow
$DllPath = "bin\Release\net472\$ProjectName.dll"
if (-not (Test-Path $DllPath)) {
    Write-Host "DLL not found at $DllPath" -ForegroundColor Red
    exit 1
}
Copy-Item $DllPath "$ReleaseDir\$ProjectName.dll"

# Create installation instructions
Write-Host "Creating installation instructions..." -ForegroundColor Yellow
$InstallText = @"
$ProjectName v$Version
==================

Installation Instructions:

1. Copy $ProjectName.dll to your Sun Haven BepInEx plugins folder:
   Sun Haven\BepInEx\plugins\$ProjectName.dll

2. Launch the game - the mod will load automatically

3. Look for the ⚔️ icon in the top-left corner of the screen
   Click it to open the Stat Editor GUI

4. Adjust sliders for combat stats and enter gold amount
   Click "Apply Changes" to save modifications

5. Changes are permanent and will save to your save file

Uninstallation:
Simply delete $ProjectName.dll from the BepInEx\plugins\ folder

For more information, visit:
https://github.com/AliesterEroan/SunHavenStatEditorMod

Built by Aliester Eroan
"@
$InstallText | Out-File -FilePath "$ReleaseDir\INSTALL.txt" -Encoding UTF8

# Copy README
Write-Host "Copying README..." -ForegroundColor Yellow
Copy-Item "README.md" "$ReleaseDir\README.md"

# Create zip file
Write-Host "Creating zip file..." -ForegroundColor Yellow
Compress-Archive -Path "$ReleaseDir\*" -DestinationPath $ZipFileName -Force

Write-Host "Build complete!" -ForegroundColor Green
Write-Host "Release package: $ZipFileName" -ForegroundColor Cyan
Write-Host "Contents:" -ForegroundColor Cyan
Get-ChildItem $ReleaseDir | ForEach-Object { Write-Host "  - $($_.Name)" }
