# Setup DeepBim-MCP add-in for Revit 2025
# Chạy script này để copy add-in vào thư mục Revit Addins

$ErrorActionPreference = "Stop"
$RevitAddinsPath = "$env:APPDATA\Autodesk\Revit\Addins\2025"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$AddInSource = Join-Path $ScriptDir "plugin\bin\AddIn 2025 Debug"

if (-not (Test-Path $AddInSource)) {
    Write-Host "Build output not found at: $AddInSource" -ForegroundColor Red
    Write-Host "Please build the solution first (Build > Build Solution in Visual Studio)" -ForegroundColor Yellow
    exit 1
}

Write-Host "Setting up DeepBim-MCP for Revit 2025..." -ForegroundColor Cyan
Write-Host "Source: $AddInSource" -ForegroundColor Gray
Write-Host "Target: $RevitAddinsPath" -ForegroundColor Gray

# Create target directory
New-Item -ItemType Directory -Path $RevitAddinsPath -Force | Out-Null

# Copy .addin manifest
$addinFile = Join-Path $AddInSource "revit-mcp-plugin.addin"
if (Test-Path $addinFile) {
    Copy-Item $addinFile -Destination $RevitAddinsPath -Force
    Write-Host "  [OK] revit-mcp-plugin.addin" -ForegroundColor Green
} else {
    Write-Host "  [SKIP] revit-mcp-plugin.addin not found" -ForegroundColor Yellow
}

# Copy revit_mcp_plugin folder (plugin DLLs + Commands)
$pluginSource = Join-Path $AddInSource "revit_mcp_plugin"
$pluginDest = Join-Path $RevitAddinsPath "revit_mcp_plugin"

if (Test-Path $pluginSource) {
    if (Test-Path $pluginDest) { Remove-Item $pluginDest -Recurse -Force }
    Copy-Item $pluginSource -Destination $pluginDest -Recurse -Force
    Write-Host "  [OK] revit_mcp_plugin\" -ForegroundColor Green
} else {
    Write-Host "  [SKIP] revit_mcp_plugin folder not found" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Setup complete! Restart Revit to load DeepBim-MCP." -ForegroundColor Green
Write-Host "Add-in will appear in Revit Add-Ins tab." -ForegroundColor Gray
