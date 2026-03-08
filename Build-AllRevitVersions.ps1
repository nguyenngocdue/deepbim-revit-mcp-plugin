# Build for all Revit versions in RevitVersions.json (revitVersions array).
# Run from repo root: .\Build-AllRevitVersions.ps1
# Optional: .\Build-AllRevitVersions.ps1 -Configuration Release

param([string] $Configuration = 'Debug')

$scriptDir = Join-Path $PSScriptRoot 'scripts'
$script = Join-Path $scriptDir 'Build-RevitVersions.ps1'
if (-not (Test-Path $script)) {
    Write-Host "Not found: $script" -ForegroundColor Red
    exit 1
}
& $script -Configuration $Configuration
