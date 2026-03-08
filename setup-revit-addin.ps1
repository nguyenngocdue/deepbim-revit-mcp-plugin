# Deploy DeepBim-MCP add-in for all Revit versions in RevitVersions.json
# Copies each "AddIn YYYY Debug" output to %APPDATA%\Autodesk\Revit\Addins\YYYY

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$versionsFile = Join-Path $ScriptDir "RevitVersions.json"

if (-not (Test-Path $versionsFile)) {
    Write-Host "RevitVersions.json not found. Create it with revitVersions array (e.g. [2024, 2025, 2026])." -ForegroundColor Red
    exit 1
}

$config = Get-Content $versionsFile -Raw -Encoding UTF8 | ConvertFrom-Json
$versionList = @($config.revitVersions) | ForEach-Object { [string]$_ }
if ($versionList.Count -eq 0) {
    Write-Host "revitVersions in RevitVersions.json is empty." -ForegroundColor Red
    exit 1
}

$Configuration = "Debug"
Write-Host "Deploying DeepBim-MCP for Revit version(s): $($versionList -join ', ')" -ForegroundColor Cyan

foreach ($ver in $versionList) {
    $AddInSource = Join-Path $ScriptDir "plugin\bin\AddIn $ver $Configuration"
    $RevitAddinsPath = "$env:APPDATA\Autodesk\Revit\Addins\$ver"

    if (-not (Test-Path $AddInSource)) {
        Write-Host "  [SKIP] Revit $ver - build output not found: $AddInSource" -ForegroundColor Yellow
        continue
    }

    Write-Host ""
    Write-Host "  Revit $ver" -ForegroundColor Green
    Write-Host "    Source: $AddInSource" -ForegroundColor Gray
    Write-Host "    Target: $RevitAddinsPath" -ForegroundColor Gray

    New-Item -ItemType Directory -Path $RevitAddinsPath -Force | Out-Null

    $addinFile = Join-Path $AddInSource "revit-mcp-plugin.addin"
    if (Test-Path $addinFile) {
        Copy-Item $addinFile -Destination $RevitAddinsPath -Force
        Write-Host "    [OK] revit-mcp-plugin.addin" -ForegroundColor Green
    } else {
        Write-Host "    [SKIP] revit-mcp-plugin.addin not found" -ForegroundColor Yellow
    }

    $pluginSource = Join-Path $AddInSource "revit_mcp_plugin"
    $pluginDest = Join-Path $RevitAddinsPath "revit_mcp_plugin"
    if (Test-Path $pluginSource) {
        if (Test-Path $pluginDest) { Remove-Item $pluginDest -Recurse -Force }
        Copy-Item $pluginSource -Destination $pluginDest -Recurse -Force
        Write-Host "    [OK] revit_mcp_plugin\" -ForegroundColor Green
        $envDest = Join-Path $pluginDest "deepbim-mcp.env.json"
        @{ mode = "deploy"; description = "Deploy: plugin uses this AppData folder. Set by setup-revit-addin.ps1." } | ConvertTo-Json | Set-Content -Path $envDest -Encoding UTF8
        Write-Host "    [OK] deepbim-mcp.env.json (mode=deploy)" -ForegroundColor Green
    } else {
        Write-Host "    [SKIP] revit_mcp_plugin folder not found" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "Deploy complete for: $($versionList -join ', '). Restart Revit to load DeepBim-MCP." -ForegroundColor Green
Write-Host "If a version was skipped, build first: .\scripts\Build-RevitVersions.ps1" -ForegroundColor Gray
