$ErrorActionPreference = "Stop"

$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    throw "未找到 C# 编译器：$csc"
}

$outDir = Join-Path $PSScriptRoot "bin"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
Get-ChildItem -Path $outDir -Filter "*.tmp.exe" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue

$datePart = Get-Date -Format "yyyyMMdd"
$versionStatePath = Join-Path $PSScriptRoot "build.version"
$buildNumber = 1
if (Test-Path $versionStatePath) {
    $state = Get-Content $versionStatePath -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($state -match '^V?(\d{8})-Build(\d{2,3})$') {
        $buildNumber = [int]$matches[2] + 1
    }
    elseif ($state -match '^V?(\d{8})(\d{3})$') {
        $buildNumber = [int]$matches[2] + 1
    }
}

$versionText = "V{0}{1:000}" -f $datePart, $buildNumber

if (-not ("NativeMethods" -as [type])) {
    Add-Type @"
using System;
using System.Runtime.InteropServices;

public static class NativeMethods
{
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool DestroyIcon(IntPtr hIcon);
}
"@
}

$versionInfoPath = Join-Path $PSScriptRoot "VersionInfo.cs"
$versionSource = @"
namespace ShowVirtualDesktopNumber
{
    internal static class BuildInfo
    {
        public const string VersionText = "$versionText";
    }
}
"@
Set-Content -Path $versionInfoPath -Value $versionSource -Encoding UTF8

$iconPath = Join-Path $PSScriptRoot "app.ico"
Add-Type -AssemblyName System.Drawing
$bitmap = New-Object System.Drawing.Bitmap 64, 64
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit
$graphics.Clear([System.Drawing.Color]::Transparent)
$background = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(32, 32, 32))
$accent = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(0, 120, 212))
$textBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
$font = New-Object System.Drawing.Font "Segoe UI", 28, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
$format = New-Object System.Drawing.StringFormat
$format.Alignment = [System.Drawing.StringAlignment]::Center
$format.LineAlignment = [System.Drawing.StringAlignment]::Center
$graphics.FillRectangle($background, 4, 4, 56, 56)
$graphics.FillRectangle($accent, 4, 48, 56, 12)
$graphics.DrawString("VD", $font, $textBrush, ([System.Drawing.RectangleF]::new(0, 0, 64, 55)), $format)
$iconHandle = $bitmap.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($iconHandle)
$stream = [System.IO.File]::Create($iconPath)
$icon.Save($stream)
$stream.Dispose()
$icon.Dispose()
$font.Dispose()
$format.Dispose()
$textBrush.Dispose()
$accent.Dispose()
$background.Dispose()
$graphics.Dispose()
$bitmap.Dispose()
[NativeMethods]::DestroyIcon($iconHandle) | Out-Null

$targetExe = Join-Path $outDir "ShowVirtualDesktopNumber.exe"
$versionedExe = Join-Path $outDir ("ShowVirtualDesktopNumber{0}.exe" -f $versionText)
$tempExe = Join-Path $outDir ("ShowVirtualDesktopNumber.{0}.tmp.exe" -f ([Guid]::NewGuid().ToString("N")))
$configFileName = "config.toml"
$rootConfigPath = Join-Path $PSScriptRoot $configFileName
$binConfigPath = Join-Path $outDir $configFileName

& $csc `
    /nologo `
    /target:winexe `
    /platform:x64 `
    /out:"$tempExe" `
    /win32icon:"$iconPath" `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    "$PSScriptRoot\Program.cs" `
    "$versionInfoPath"

if ($LASTEXITCODE -ne 0) {
    throw "C# compiler failed with exit code $LASTEXITCODE."
}

Copy-Item -Force -Path $tempExe -Destination $versionedExe
$targetUpdated = $true
try {
    Copy-Item -Force -Path $tempExe -Destination $targetExe
}
catch {
    $targetUpdated = $false
    Write-Warning "Fixed executable was not updated because it is in use: $targetExe"
}
Remove-Item -Force -Path $tempExe
if (-not (Test-Path $rootConfigPath)) {
    Set-Content -Path $rootConfigPath -Value @(
        "# ShowVirtualDesktopNumber configuration",
        "# language: zh or en",
        'language = "zh"'
    ) -Encoding ASCII
}
if (-not (Test-Path $binConfigPath)) {
    Copy-Item -Path $rootConfigPath -Destination $binConfigPath
}
Set-Content -Path $versionStatePath -Value $versionText -Encoding ASCII

if ($targetUpdated) {
    Write-Host "Built: $targetExe"
}
else {
    Write-Host "Built: fixed executable skipped because it is in use"
}
Write-Host "Versioned copy: $versionedExe"
Write-Host "Config: $binConfigPath"
Write-Host "Version: $versionText"
