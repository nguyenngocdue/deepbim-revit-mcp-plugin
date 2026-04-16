<#
.SYNOPSIS
    Build MSI installer(s) for DeepBim-MCP Revit Plugin.

.DESCRIPTION
    This script builds the plugin solution and then creates MSI installer(s)
    for the specified Revit version(s).

.PARAMETER Versions
    Comma-separated Revit versions to build. Default: reads from RevitVersions.json.
    Example: -Versions 2024,2025

.PARAMETER ProductVersion
    Product version for the installer. Default: 1.0.0
    Example: -ProductVersion 1.2.0

.PARAMETER Configuration
    Build configuration. Default: Release
    Example: -Configuration Debug

.PARAMETER SkipBuild
    Skip building the plugin solution (use existing build output).

.EXAMPLE
    # Build MSI for Revit 2025
    .\Build-Installer.ps1 -Versions 2025

    # Build MSIs for multiple versions
    .\Build-Installer.ps1 -Versions 2024,2025

    # Build all versions from RevitVersions.json
    .\Build-Installer.ps1

    # Build with specific product version
    .\Build-Installer.ps1 -Versions 2025 -ProductVersion 2.0.0

    # Skip plugin build (use existing output)
    .\Build-Installer.ps1 -Versions 2025 -SkipBuild
#>

param(
    [string[]]$Versions,
    [string]$ProductVersion = "1.0.0",
    [string]$Configuration = "Release",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

# Paths
$scriptDir = $PSScriptRoot
$rootDir = Resolve-Path "$scriptDir\..\.."
$solutionFile = Join-Path $rootDir "revit-mcp-plugin.sln"
$wixProjectFile = Join-Path $scriptDir "DeepBimMCP.Installer.wixproj"
$outputDir = Join-Path $scriptDir "output"
$versionsJsonFile = Join-Path $rootDir "RevitVersions.json"

# If no versions specified, read from RevitVersions.json
if (-not $Versions -or $Versions.Count -eq 0) {
    if (Test-Path $versionsJsonFile) {
        $versionsJson = Get-Content $versionsJsonFile -Raw | ConvertFrom-Json
        $Versions = $versionsJson.revitVersions | ForEach-Object { $_.ToString() }
        Write-Host "Read versions from RevitVersions.json: $($Versions -join ', ')" -ForegroundColor Cyan
    } else {
        $Versions = @("2025")
        Write-Host "No RevitVersions.json found. Defaulting to Revit 2025." -ForegroundColor Yellow
    }
}

# Ensure output directory exists
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  DeepBim-MCP MSI Installer Builder" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Versions     : $($Versions -join ', ')"
Write-Host "  Product Ver  : $ProductVersion"
Write-Host "  Configuration: $Configuration"
Write-Host "  Output       : $outputDir"
Write-Host "========================================" -ForegroundColor Green
Write-Host ""

# Step 0: Install server deps + TypeScript build, then generate WiX fragments
Write-Host "[0] Preparing MCP server for MSI (npm ci, npm run build, WiX fragments)..." -ForegroundColor Yellow
$serverDir = Join-Path $rootDir "server"
$genServerFiles = Join-Path $scriptDir "Generate-ServerFiles.ps1"
$genNodeModules = Join-Path $scriptDir "Generate-ServerNodeModules.ps1"

if (-not (Test-Path (Join-Path $serverDir "package-lock.json"))) {
    Write-Host "  FAILED: server/package-lock.json not found." -ForegroundColor Red
    exit 1
}

$prepareBranding = Join-Path $scriptDir "Prepare-InstallerBranding.ps1"
if (Test-Path $prepareBranding) {
    Write-Host "  Preparing MSI branding (banner: -56 right-aligned; dialog: -512 or main logo)..." -ForegroundColor DarkGray
    & $prepareBranding
} else {
    Write-Host "  WARNING: Prepare-InstallerBranding.ps1 not found." -ForegroundColor Yellow
}

if ($env:OS -ne "Windows_NT") {
    Write-Host "  WARNING: Run this script on Windows (not WSL/Linux) so npm installs win32 native binaries for better-sqlite3." -ForegroundColor Yellow
    Write-Host "           An MSI built with Linux node_modules will fail at runtime on Revit users' PCs." -ForegroundColor Yellow
}

Push-Location $serverDir
try {
    # Full install: typescript + rimraf are devDependencies; required for `npm run build`.
    # After build, prune so WiX only packages runtime deps (smaller MSI, no dev tools in install dir).
    npm ci
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  FAILED: npm ci in server/ failed (network or lockfile)." -ForegroundColor Red
        exit 1
    }
    npm run build
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  FAILED: npm run build in server/ failed." -ForegroundColor Red
        exit 1
    }
    npm prune --omit=dev
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  FAILED: npm prune --omit=dev in server/ failed." -ForegroundColor Red
        exit 1
    }
} finally {
    Pop-Location
}

if (-not (Test-Path (Join-Path $serverDir "build\index.js"))) {
    Write-Host "  FAILED: server/build/index.js missing after build." -ForegroundColor Red
    exit 1
}

if (Test-Path $genNodeModules) {
    & $genNodeModules
} else {
    Write-Host "  WARNING: Generate-ServerNodeModules.ps1 not found. Skipping." -ForegroundColor Yellow
}

if (Test-Path $genServerFiles) {
    & $genServerFiles
} else {
    Write-Host "  WARNING: Generate-ServerFiles.ps1 not found. Skipping." -ForegroundColor Yellow
}

$successCount = 0
$failCount = 0

foreach ($ver in $Versions) {
    Write-Host ""
    Write-Host "----------------------------------------" -ForegroundColor Cyan
    Write-Host "  Building for Revit $ver" -ForegroundColor Cyan
    Write-Host "----------------------------------------" -ForegroundColor Cyan

    # Step 1: Build the plugin solution
    if (-not $SkipBuild) {
        Write-Host ""
        Write-Host "[1/2] Building plugin for Revit $ver..." -ForegroundColor Yellow

        $buildArgs = @(
            "build"
            $solutionFile
            "-c", "$Configuration"
            "-p:RevitVersion=$ver"
            "--verbosity", "minimal"
        )

        & dotnet @buildArgs
        if ($LASTEXITCODE -ne 0) {
            Write-Host "  FAILED: Plugin build for Revit $ver failed!" -ForegroundColor Red
            $failCount++
            continue
        }
        Write-Host "  Plugin build succeeded." -ForegroundColor Green
    } else {
        Write-Host "[1/2] Skipping plugin build (using existing output)." -ForegroundColor DarkGray
    }

    # Verify build output exists
    $pluginSourceDir = Join-Path $rootDir "plugin\bin\AddIn $ver $Configuration"
    if (-not (Test-Path $pluginSourceDir)) {
        Write-Host "  FAILED: Build output not found at: $pluginSourceDir" -ForegroundColor Red
        Write-Host "  Make sure the plugin was built with Configuration=$Configuration and RevitVersion=$ver" -ForegroundColor Red
        $failCount++
        continue
    }

    # Step 2: Build the MSI
    Write-Host ""
    Write-Host "[2/2] Building MSI installer for Revit $ver..." -ForegroundColor Yellow

    $msiOutputName = "deepbim-mcp-revit$ver-v$ProductVersion.msi"
    $msiArgs = @(
        "build"
        $wixProjectFile
        "-p:RevitVersion=$ver"
        "-p:ProductVersion=$ProductVersion"
        "-p:Configuration=$Configuration"
        "-p:OutputName=DeepBimMCP-Revit$ver-v$ProductVersion"
        "--verbosity", "minimal"
    )

    & dotnet @msiArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  FAILED: MSI build for Revit $ver failed!" -ForegroundColor Red
        $failCount++
        continue
    }

    # Find and copy the generated MSI to output folder
    $msiSearchPaths = @(
        (Join-Path $scriptDir "bin\$Configuration\*.msi"),
        (Join-Path $scriptDir "bin\*.msi")
    )

    $msiFile = $null
    foreach ($searchPath in $msiSearchPaths) {
        $found = Get-ChildItem -Path $searchPath -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($found) {
            $msiFile = $found
            break
        }
    }

    if ($msiFile) {
        $destPath = Join-Path $outputDir $msiOutputName
        Copy-Item $msiFile.FullName $destPath -Force
        Write-Host "  MSI created: $destPath" -ForegroundColor Green
        $successCount++
    } else {
        Write-Host "  WARNING: MSI file not found in expected location." -ForegroundColor Yellow
        Write-Host "  Check bin\ folder for the generated MSI." -ForegroundColor Yellow
        $failCount++
    }
}

# Summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Build Summary" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Succeeded: $successCount" -ForegroundColor $(if ($successCount -gt 0) { "Green" } else { "Gray" })
Write-Host "  Failed   : $failCount" -ForegroundColor $(if ($failCount -gt 0) { "Red" } else { "Gray" })

if ($successCount -gt 0) {
    Write-Host ""
    Write-Host "  Output folder: $outputDir" -ForegroundColor Cyan
    Write-Host ""
    Get-ChildItem -Path $outputDir -Filter "*.msi" | ForEach-Object {
        $sizeMB = [math]::Round($_.Length / 1MB, 2)
        Write-Host "    $($_.Name)  ($sizeMB MB)" -ForegroundColor White
    }
}

Write-Host ""

if ($failCount -gt 0) {
    exit 1
}
