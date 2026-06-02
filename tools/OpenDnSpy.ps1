# OpenDnSpy.ps1 — downloads dnSpy (if not already present) and opens
# 7DTD's Assembly-CSharp.dll ready for browsing.
#
# Usage: .\OpenDnSpy.ps1 [-GameDir "C:\...\7 Days To Die"]

param(
    [string]$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die"
)

$toolsDir = $PSScriptRoot
$dnSpyDir = Join-Path $toolsDir "dnSpy"
$dnSpyExe = Join-Path $dnSpyDir "dnSpy.exe"
$dllPath  = Join-Path $GameDir "7DaysToDie_Data\Managed\Assembly-CSharp.dll"

# ── Download dnSpy if not already present ───────────────────────────────────
if (-not (Test-Path $dnSpyExe)) {
    Write-Host "dnSpy not found — downloading..." -ForegroundColor Cyan
    $zipUrl  = "https://github.com/dnSpy/dnSpy/releases/download/v6.1.8/dnSpy-net-win64.zip"
    $zipPath = Join-Path $toolsDir "dnSpy.zip"

    Invoke-WebRequest -Uri $zipUrl -OutFile $zipPath -UseBasicParsing
    Write-Host "Extracting..." -ForegroundColor Cyan
    Expand-Archive -Path $zipPath -DestinationPath $dnSpyDir -Force
    Remove-Item $zipPath
    Write-Host "dnSpy ready." -ForegroundColor Green
} else {
    Write-Host "dnSpy already installed." -ForegroundColor Green
}

# ── Check the DLL exists ─────────────────────────────────────────────────────
if (-not (Test-Path $dllPath)) {
    Write-Host "Assembly-CSharp.dll not found at: $dllPath" -ForegroundColor Red
    Write-Host "Pass -GameDir with the correct path." -ForegroundColor Yellow
    exit 1
}

# ── Launch dnSpy with the DLL ─────────────────────────────────────────────────
Write-Host "Opening dnSpy -> $dllPath" -ForegroundColor Cyan
Start-Process $dnSpyExe -ArgumentList "`"$dllPath`""
