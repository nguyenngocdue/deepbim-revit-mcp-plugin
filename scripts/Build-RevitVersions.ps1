<#
.SYNOPSIS
  Build the Revit MCP plugin for one or more Revit versions (2019-2026).

.DESCRIPTION
  Uses RevitVersion MSBuild property. Versions can be:
  - From RevitVersions.json (revitVersions array)
  - From -Versions "2024,2025,2026"

.EXAMPLE
  .\Build-RevitVersions.ps1
  # Builds versions listed in RevitVersions.json

.EXAMPLE
  .\Build-RevitVersions.ps1 -Versions 2024,2025,2026
  # Builds 2024, 2025, 2026

.EXAMPLE
  .\Build-RevitVersions.ps1 -Versions 2025 -Configuration Release
  # Single version, Release build
#>
param(
    [string[]] $Versions = @(),
    [string]  $Configuration = 'Debug',
    [string]  $SolutionPath = $null
)

$ErrorActionPreference = 'Stop'
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir  = Split-Path -Parent $scriptDir

if (-not $SolutionPath) {
    $SolutionPath = Join-Path $rootDir 'revit-mcp-plugin.sln'
}

# Keep RevitVersion.generated.props in sync with RevitVersions.json (defaultVersion)
$generateScript = Join-Path $scriptDir 'Generate-RevitVersionProps.ps1'
if (Test-Path $generateScript) {
    & $generateScript
}

# Resolve versions: -Versions or RevitVersions.json
$versionList = @()
if ($Versions.Count -gt 0) {
    $versionList = $Versions
} else {
    $versionsFile = Join-Path $rootDir 'RevitVersions.json'
    if (Test-Path $versionsFile) {
        try {
            $config = Get-Content $versionsFile -Raw -Encoding UTF8 | ConvertFrom-Json
            $raw = $config.revitVersions
            $versionList = @($raw) | ForEach-Object { [string]$_ }
        } catch {
            Write-Host "Invalid RevitVersions.json: $_" -ForegroundColor Red
            exit 1
        }
    }
    if ($versionList.Count -eq 0) {
        Write-Host "No versions specified. Use -Versions 2024,2025 or set revitVersions in RevitVersions.json." -ForegroundColor Yellow
        exit 1
    }
}

Write-Host "Building for Revit version(s): $($versionList -join ', ')" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Cyan

foreach ($ver in $versionList) {
    Write-Host "`n--- Revit $ver ---" -ForegroundColor Green
    $r = dotnet build $SolutionPath -c $Configuration -p:RevitVersion=$ver --no-incremental 2>&1
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        $r | Write-Host
        Write-Host "Build failed for Revit $ver (exit code $exitCode)." -ForegroundColor Red
        exit $exitCode
    }
    Write-Host "Revit $ver OK." -ForegroundColor Green
}

Write-Host "`nAll builds completed." -ForegroundColor Cyan
