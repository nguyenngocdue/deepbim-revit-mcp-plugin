<#
.SYNOPSIS
  Build one Revit add-in payload and export one MSI.

.EXAMPLE
  .\scripts\Build-OneMsi.ps1 -RevitVersion 2026 -ProductVersion 3.0.0

.EXAMPLE
  .\scripts\Build-OneMsi.ps1 -RevitVersion 2025 -ProductVersion 3.0.0 -Configuration Release
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d{4}$')]
    [string] $RevitVersion,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string] $ProductVersion,

    [string] $Configuration = 'Release',
    [switch] $SkipPluginBuild
)

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir
$buildPluginScript = Join-Path $scriptDir 'Build-RevitVersions.ps1'
$buildInstallerScript = Join-Path $rootDir 'installers\msi\Build-Installer.ps1'
$addinRoot = Join-Path $rootDir "plugin\bin\AddIn $RevitVersion $Configuration"
$addinFolder = Join-Path $addinRoot 'DeepBimRevitMCPlugin'
$commandSetDll = Join-Path $addinFolder "Commands\RevitMCPCommandSet\$RevitVersion\RevitMCPCommandSet.dll"
$msiPath = Join-Path $rootDir "installers\msi\output\DeepBimMCP-Revit$RevitVersion-v$ProductVersion.msi"

function Assert-PathExists {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $Message
    )

    if (-not (Test-Path $Path)) {
        throw "$Message`nMissing: $Path"
    }
}

Write-Host "Build one MSI" -ForegroundColor Cyan
Write-Host "RevitVersion: $RevitVersion | ProductVersion: $ProductVersion | Configuration: $Configuration" -ForegroundColor Cyan

Assert-PathExists $buildPluginScript "Plugin build script not found."
Assert-PathExists $buildInstallerScript "MSI build script not found."

if (-not $SkipPluginBuild) {
    Write-Host "`n[1/3] Building add-in payload..." -ForegroundColor Green
    & $buildPluginScript -Versions $RevitVersion -Configuration $Configuration
    if (-not $?) {
        throw "Add-in build failed for Revit $RevitVersion."
    }
} else {
    Write-Host "`n[1/3] Skipping add-in build; using existing payload." -ForegroundColor Yellow
}

Write-Host "`n[2/3] Verifying add-in payload..." -ForegroundColor Green
Assert-PathExists (Join-Path $addinRoot 'DeepBimRevitMCPlugin.addin') "Add-in manifest was not generated."
Assert-PathExists (Join-Path $addinFolder 'RevitMCPPlugin.dll') "Main plugin DLL was not generated."
Assert-PathExists (Join-Path $addinFolder 'RevitMCPSDK.dll') "Revit MCP SDK DLL was not generated."
Assert-PathExists (Join-Path $addinFolder 'CommandDeepBimMCPTools.dll') "Tool command DLL was not generated."
Assert-PathExists (Join-Path $addinFolder 'Commands\commandRegistry.json') "Command registry was not generated."
Assert-PathExists (Join-Path $addinFolder 'Commands\RevitMCPCommandSet\command.json') "Command manifest was not generated."
Assert-PathExists $commandSetDll "Command set DLL was not generated."
Write-Host "Payload OK: $addinRoot" -ForegroundColor Green

Write-Host "`n[3/3] Building MSI..." -ForegroundColor Green
& $buildInstallerScript -Versions $RevitVersion -ProductVersion $ProductVersion -Configuration $Configuration
if (-not $?) {
    throw "MSI build failed for Revit $RevitVersion."
}

Assert-PathExists $msiPath "MSI was not copied to output."

Write-Host "`nDone." -ForegroundColor Cyan
Write-Host "MSI: $msiPath" -ForegroundColor Green
