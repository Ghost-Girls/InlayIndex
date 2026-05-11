param(
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

$vsRoot = "$env:LocalAppData\Microsoft\VisualStudio"
$extensionsDir = $null

Write-Host "=== Scanning VS Extension Directories ===" -ForegroundColor Cyan

if (-not (Test-Path $vsRoot)) {
    Write-Host "ERROR: $vsRoot not found" -ForegroundColor Red
    exit 1
}

$vsDirs = Get-ChildItem -Path $vsRoot -Directory | Where-Object { $_.Name -match '^\d+\.\d+' }
foreach ($vsDir in $vsDirs) {
    $extPath = Join-Path $vsDir.FullName "Extensions"
    if (Test-Path $extPath) {
        Write-Host "  Found: $extPath" -ForegroundColor Gray
        $extensionsDir = $extPath
        break
    }
}

if (-not $extensionsDir) {
    Write-Host "ERROR: Extensions directory not found" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "=== Scanning for InlayIndex copies ===" -ForegroundColor Cyan

$inlayDirs = @()
$allSubDirs = Get-ChildItem -Path $extensionsDir -Directory

foreach ($dir in $allSubDirs) {
    $dllPath = Join-Path $dir.FullName "InlayIndex.dll"
    $manifestPath = Join-Path $dir.FullName "extension.vsixmanifest"
    $catalogPath = Join-Path $dir.FullName "catalog.json"
    
    $isInlay = $false
    if (Test-Path $dllPath) { $isInlay = $true }
    if (-not $isInlay -and (Test-Path $manifestPath)) {
        $content = Get-Content $manifestPath -Raw -ErrorAction SilentlyContinue
        if ($content -match "InlayIndex") { $isInlay = $true }
    }
    if (-not $isInlay -and (Test-Path $catalogPath)) {
        $content = Get-Content $catalogPath -Raw -ErrorAction SilentlyContinue
        if ($content -match "InlayIndex") { $isInlay = $true }
    }
    
    if ($isInlay) {
        $inlayDirs += $dir
        Write-Host "  [FOUND] $($dir.Name)" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "Total InlayIndex copies found: $($inlayDirs.Count)" -ForegroundColor $(if ($inlayDirs.Count -gt 0) { "Yellow" } else { "Green" })

if ($inlayDirs.Count -eq 0) {
    Write-Host "No old copies to clean up." -ForegroundColor Green
    exit 0
}

$keepLatest = $inlayDirs | Sort-Object LastWriteTime -Descending | Select-Object -First 1
$toDelete = $inlayDirs | Where-Object { $_.FullName -ne $keepLatest.FullName }

Write-Host "Keep latest: $($keepLatest.Name) (LastWrite: $($keepLatest.LastWriteTime))" -ForegroundColor Green
Write-Host "Will delete $($toDelete.Count) old copies" -ForegroundColor Red

if ($WhatIf) {
    Write-Host ""
    Write-Host "=== Preview Mode (WhatIf) - will NOT delete ===" -ForegroundColor Cyan
    foreach ($d in $toDelete) {
        Write-Host "  [WOULD DELETE] $($d.Name) (LastWrite: $($d.LastWriteTime))" -ForegroundColor Magenta
    }
    Write-Host ""
    Write-Host "To actually delete, run: .\CleanExtension.ps1" -ForegroundColor Green
}
else {
    Write-Host ""
    Write-Host "=== Deleting ===" -ForegroundColor Cyan
    foreach ($d in $toDelete) {
        try {
            Remove-Item -Path $d.FullName -Recurse -Force
            Write-Host "  [DELETED] $($d.Name)" -ForegroundColor Green
        }
        catch {
            Write-Host "  [FAILED] $($d.Name) : $_" -ForegroundColor Red
        }
    }
    Write-Host ""
    Write-Host "=== Deletion complete ===" -ForegroundColor Green
}

Write-Host ""
Write-Host "=== DONE ===" -ForegroundColor Green
Write-Host "Make sure VS is fully closed, then reinstall the VSIX." -ForegroundColor Yellow