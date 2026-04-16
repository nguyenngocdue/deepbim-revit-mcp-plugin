<#
.SYNOPSIS
    Builds WiX WixUIBanner (493x58) and WixUIDialog (493x312) from plugin Resources.

    Assets:
      - deepbim-logo-56.png  → banner (compact mark, drawn in the RIGHT zone only)
      - deepbim-logo-512.png → dialog when present (sharp on welcome), else deepbim-logo.png

    WiX inner dialogs reserve the LEFT part of the banner for title text; a centered full-width
    logo overlaps that text. This script keeps the left ~275px white and right-aligns the mark.

.EXAMPLE
    .\Prepare-InstallerBranding.ps1
#>

$ErrorActionPreference = "Stop"

$scriptDir = $PSScriptRoot
$resDir = Join-Path $scriptDir "..\..\plugin\Resources"
$logoPath = (Resolve-Path (Join-Path $resDir "deepbim-logo.png")).Path
$logo56Path = Join-Path $resDir "deepbim-logo-56.png"
$logo512Path = Join-Path $resDir "deepbim-logo-512.png"

$bannerSource = if (Test-Path $logo56Path) { (Resolve-Path $logo56Path).Path } else { $logoPath }
$dialogSource = if (Test-Path $logo512Path) { (Resolve-Path $logo512Path).Path } else { $logoPath }

$outDir = Join-Path $scriptDir "branding"
if (-not (Test-Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}

$bannerW, $bannerH = 493, 58
$dialogW, $dialogH = 493, 312

# Pixels reserved on the left of the banner for WiX title / subtitle (do not paint logo here)
$bannerLeftReserve = 275
$bannerPadRight = 14
$bannerPadVert = 4

Add-Type -AssemblyName System.Drawing

function Write-BannerRightAlignedPng {
    param(
        [string]$SourcePath,
        [string]$DestPath,
        [int]$TargetWidth,
        [int]$TargetHeight,
        [int]$LeftReserve,
        [int]$PadRight,
        [int]$PadVert
    )

    $src = $null
    $bmp = $null
    $g = $null
    try {
        $src = [System.Drawing.Image]::FromFile($SourcePath)
        $bmp = New-Object System.Drawing.Bitmap $TargetWidth, $TargetHeight
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $g.Clear([System.Drawing.Color]::White)

        $zoneW = $TargetWidth - $LeftReserve - $PadRight
        $zoneH = $TargetHeight - (2 * $PadVert)
        if ($zoneW -lt 32) { $zoneW = 32 }
        if ($zoneH -lt 16) { $zoneH = 16 }

        $scale = [Math]::Min($zoneW / $src.Width, $zoneH / $src.Height)
        $w = [Math]::Max(1, [int]([Math]::Round($src.Width * $scale)))
        $h = [Math]::Max(1, [int]([Math]::Round($src.Height * $scale)))
        $x = $TargetWidth - $PadRight - $w
        $y = [int]([Math]::Round(($TargetHeight - $h) / 2.0))
        $g.DrawImage($src, $x, $y, $w, $h)
        $bmp.Save($DestPath, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        if ($g) { $g.Dispose() }
        if ($bmp) { $bmp.Dispose() }
        if ($src) { $src.Dispose() }
    }
}

function Write-FittedCenterPng {
    param(
        [string]$SourcePath,
        [string]$DestPath,
        [int]$TargetWidth,
        [int]$TargetHeight
    )

    $src = $null
    $bmp = $null
    $g = $null
    try {
        $src = [System.Drawing.Image]::FromFile($SourcePath)
        $bmp = New-Object System.Drawing.Bitmap $TargetWidth, $TargetHeight
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $g.Clear([System.Drawing.Color]::White)

        $scale = [Math]::Min($TargetWidth / $src.Width, $TargetHeight / $src.Height)
        $w = [Math]::Max(1, [int]([Math]::Round($src.Width * $scale)))
        $h = [Math]::Max(1, [int]([Math]::Round($src.Height * $scale)))
        $x = [int]([Math]::Round(($TargetWidth - $w) / 2.0))
        $y = [int]([Math]::Round(($TargetHeight - $h) / 2.0))
        $g.DrawImage($src, $x, $y, $w, $h)
        $bmp.Save($DestPath, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        if ($g) { $g.Dispose() }
        if ($bmp) { $bmp.Dispose() }
        if ($src) { $src.Dispose() }
    }
}

$bannerOut = Join-Path $outDir "WixUIBanner.png"
$dialogOut = Join-Path $outDir "WixUIDialog.png"

Write-Host "Banner source: $bannerSource (right zone only, left ${bannerLeftReserve}px clear)" -ForegroundColor Cyan
Write-Host "Dialog source: $dialogSource" -ForegroundColor Cyan

Write-BannerRightAlignedPng -SourcePath $bannerSource -DestPath $bannerOut `
    -TargetWidth $bannerW -TargetHeight $bannerH `
    -LeftReserve $bannerLeftReserve -PadRight $bannerPadRight -PadVert $bannerPadVert

Write-FittedCenterPng -SourcePath $dialogSource -DestPath $dialogOut -TargetWidth $dialogW -TargetHeight $dialogH

Write-Host "Written: $bannerOut (${bannerW}x${bannerH})" -ForegroundColor Green
Write-Host "Written: $dialogOut (${dialogW}x${dialogH})" -ForegroundColor Green
