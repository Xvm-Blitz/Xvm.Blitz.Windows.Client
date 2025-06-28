# Build XVM Blitz Windows Client Installer
# Uses WiX Toolset v6.0.0

Write-Host "Building XVM Blitz Windows Client Installer..." -ForegroundColor Green

if (-not (Get-Command "wix" -ErrorAction SilentlyContinue)) {
    Write-Host "Wix Toolset not found. Installing..." -ForegroundColor Yellow
    dotnet tool install --global wix --version 6.0.0
}

$wixVersion = wix --version
Write-Host "Using WiX version: $wixVersion" -ForegroundColor Cyan

Write-Host "Publishing application..." -ForegroundColor Yellow
Set-Location "..\Xvm.Blitz.Windows.Client.UI"
dotnet publish -c Release -r win-x64 --self-contained true

Set-Location "..\Xvm.Blitz.Windows.Client.Installer"

Write-Host "Cleaning previous build artifacts..." -ForegroundColor Yellow
Remove-Item -Path "Xmv Blitz.msi" -ErrorAction SilentlyContinue
Remove-Item -Path "*.exe" -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force bin, obj -ErrorAction SilentlyContinue

Write-Host "Building installer with WiX v6.0.0..." -ForegroundColor Yellow

Write-Host "Using dotnet build command..." -ForegroundColor Green
dotnet build -c Release

if ($LASTEXITCODE -eq 0) {
    Write-Host "Installer built successfully using 'dotnet build'!" -ForegroundColor Green
    $msiFile = Get-ChildItem "Xmv Blitz.msi" -ErrorAction SilentlyContinue
    if ($msiFile) {
        $sizeKB = ($msiFile.Length / 1024).ToString('F1')
        Write-Host "Created: $($msiFile.Name) ($sizeKB KB)" -ForegroundColor Cyan
    }
}

Write-Host "Build process completed." -ForegroundColor Green
