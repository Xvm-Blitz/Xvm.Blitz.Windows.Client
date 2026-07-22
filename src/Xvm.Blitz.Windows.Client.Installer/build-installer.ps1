Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "Сборка инсталлятора XVM Blitz..." -ForegroundColor Green

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptDir "..\..")
$uiProject = Join-Path $repoRoot "src\Xvm.Blitz.Windows.Client.UI\Xvm.Blitz.Windows.Client.UI.csproj"
$installerDir = Join-Path $repoRoot "src\Xvm.Blitz.Windows.Client.Installer"

if (-not (Get-Command "wix" -ErrorAction SilentlyContinue)) {
    Write-Host "WiX не найден, устанавливаю..." -ForegroundColor Yellow
    dotnet tool install --global wix --version 6.0.0
}

Write-Host "WiX: $(wix --version)" -ForegroundColor Cyan

Write-Host "Публикация приложения..." -ForegroundColor Yellow
dotnet publish $uiProject -c Release -r win-x64 --self-contained true
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish завершился с ошибкой"
}

Set-Location $installerDir

Write-Host "Очистка предыдущей сборки..." -ForegroundColor Yellow
Remove-Item -Path "XVMBlitzSetup.msi" -ErrorAction SilentlyContinue
Remove-Item -Path "XVM Blitz.msi" -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force bin, obj -ErrorAction SilentlyContinue

Write-Host "Сборка MSI (WiX)..." -ForegroundColor Yellow
dotnet build -c Release
if ($LASTEXITCODE -ne 0) {
    throw "Сборка MSI завершилась с ошибкой"
}

$msiFile = Get-ChildItem -Path $installerDir, (Join-Path $installerDir "bin\Release") -Filter "*.msi" -Recurse -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $msiFile) {
    throw "MSI не найден после сборки"
}

$sizeMb = ($msiFile.Length / 1MB).ToString("F1")
Write-Host "Готово: $($msiFile.FullName) ($sizeMb MB)" -ForegroundColor Cyan
Write-Host "Подпись релизов выполняется в CI через SignPath Foundation." -ForegroundColor DarkGray
