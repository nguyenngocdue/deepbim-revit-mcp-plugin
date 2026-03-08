# Generate RevitVersion.generated.props from RevitVersions.json (defaultVersion or first of revitVersions).
# Run after editing RevitVersions.json so the default build version is in sync.
# Build uses this file when you select "Debug" or "Release" (no version suffix).

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir
$jsonPath = Join-Path $rootDir "RevitVersions.json"
$outPath = Join-Path $rootDir "RevitVersion.generated.props"

if (-not (Test-Path $jsonPath)) {
    Write-Host "RevitVersions.json not found. Create it with defaultVersion and revitVersions." -ForegroundColor Red
    exit 1
}

$config = Get-Content $jsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
$version = $null
if ($config.PSObject.Properties.Name -contains "defaultVersion") {
    $version = [string]$config.defaultVersion
}
if (-not $version -and $config.revitVersions) {
    $first = @($config.revitVersions)[0]
    $version = [string]$first
}
if (-not $version) {
    Write-Host "RevitVersions.json has no defaultVersion or revitVersions." -ForegroundColor Red
    exit 1
}

$content = @"
<?xml version="1.0" encoding="utf-8"?>
<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <RevitVersion>$version</RevitVersion>
    <RevitInstallPath>C:\Program Files\Autodesk\Revit $version\Revit.exe</RevitInstallPath>
  </PropertyGroup>
</Project>
"@
Set-Content -Path $outPath -Value $content -Encoding UTF8
Write-Host "RevitVersion.generated.props updated: RevitVersion=$version" -ForegroundColor Green
