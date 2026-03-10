# Deploy DeepBim-MCP add-in for all Revit versions in RevitVersions.json
# Copies each "AddIn YYYY Debug" output to %APPDATA%\Autodesk\Revit\Addins\YYYY

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$versionsFile = Join-Path $ScriptDir "RevitVersions.json"
$AddinManifestName = "DeepBimRevitMCPlugin.addin"
$AddinFolderName = "DeepBimRevitMCPlugin"
$LegacyAddinManifestName = "revit-mcp-plugin.addin"
$LegacyAddinFolderName = "revit_mcp_plugin"

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
    $legacyAddinPath = Join-Path $RevitAddinsPath $LegacyAddinManifestName
    if (Test-Path $legacyAddinPath) {
        Remove-Item $legacyAddinPath -Force
        Write-Host "    [OK] Removed legacy manifest: $LegacyAddinManifestName" -ForegroundColor DarkGray
    }

    $legacyPluginPath = Join-Path $RevitAddinsPath $LegacyAddinFolderName
    if (Test-Path $legacyPluginPath) {
        Remove-Item $legacyPluginPath -Recurse -Force
        Write-Host "    [OK] Removed legacy folder: $LegacyAddinFolderName\" -ForegroundColor DarkGray
    }

    $addinFile = Join-Path $AddInSource $AddinManifestName
    if (Test-Path $addinFile) {
        Copy-Item $addinFile -Destination $RevitAddinsPath -Force
        Write-Host "    [OK] $AddinManifestName" -ForegroundColor Green
    } else {
        Write-Host "    [SKIP] $AddinManifestName not found" -ForegroundColor Yellow
    }

    $pluginSource = Join-Path $AddInSource $AddinFolderName
    $pluginDest = Join-Path $RevitAddinsPath $AddinFolderName
    if (Test-Path $pluginSource) {
        if (Test-Path $pluginDest) { Remove-Item $pluginDest -Recurse -Force }
        Copy-Item $pluginSource -Destination $pluginDest -Recurse -Force
        Write-Host "    [OK] $AddinFolderName\" -ForegroundColor Green
        $envDest = Join-Path $pluginDest "deepbim-mcp.env.json"
        @{ mode = "deploy"; description = "Deploy: plugin uses this AppData folder. Set by setup-revit-addin.ps1." } | ConvertTo-Json | Set-Content -Path $envDest -Encoding UTF8
        Write-Host "    [OK] deepbim-mcp.env.json (mode=deploy)" -ForegroundColor Green
    } else {
        Write-Host "    [SKIP] $AddinFolderName folder not found" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "Deploy complete for: $($versionList -join ', '). Restart Revit to load DeepBim-MCP." -ForegroundColor Green
Write-Host "If a version was skipped, build first: .\scripts\Build-RevitVersions.ps1" -ForegroundColor Gray
