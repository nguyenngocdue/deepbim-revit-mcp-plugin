<#
.SYNOPSIS
  Copy an already-built add-in payload (plugin\bin\AddIn <ver> <cfg>\) into %APPDATA%\Autodesk\Revit\Addins\<ver>.

.DESCRIPTION
  Use after `dotnet build ... -p:RevitVersion=<ver>` when Revit was running during the build (locked DLLs).
  Refuses to run while Revit.exe of that version is open unless -Force. Never deletes files; overwrites in place.

.EXAMPLE
  .\scripts\Deploy-AddIn.ps1 -RevitVersion 2024 -Configuration Release
#>
param(
    [Parameter(Mandatory = $true)][string] $RevitVersion,
    [string] $Configuration = 'Release',
    [switch] $Force
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$src = Join-Path $root "plugin\bin\AddIn $RevitVersion $Configuration"
$dstRoot = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$RevitVersion"

if (-not (Test-Path (Join-Path $src 'DeepBimRevitMCPlugin.addin'))) {
    throw "Payload not found: $src  (build first: dotnet build commandset\RevitMCPCommandSet.csproj -c $Configuration -p:RevitVersion=$RevitVersion)"
}

$revit = Get-Process -Name Revit -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "*Revit $RevitVersion*" }
if ($revit -and -not $Force) {
    throw "Revit $RevitVersion is running (PID $($revit.Id -join ',')). Close it first, then re-run. (-Force to try anyway)"
}

New-Item -ItemType Directory -Force -Path $dstRoot | Out-Null
Copy-Item -Path (Join-Path $src 'DeepBimRevitMCPlugin.addin') -Destination $dstRoot -Force
Copy-Item -Path (Join-Path $src 'DeepBimRevitMCPlugin') -Destination $dstRoot -Recurse -Force

$dll = Join-Path $dstRoot "DeepBimRevitMCPlugin\Commands\RevitMCPCommandSet\$RevitVersion\RevitMCPCommandSet.dll"
$reg = Join-Path $dstRoot "DeepBimRevitMCPlugin\Commands\commandRegistry.json"
Write-Host "Deployed to $dstRoot" -ForegroundColor Green
Write-Host ("  CommandSet DLL : {0}  ({1})" -f $dll, (Get-Item $dll).LastWriteTime)
Write-Host ("  rcd_* commands : {0}" -f ((Select-String -Path $reg -Pattern 'rcd_' -AllMatches).Matches.Count))
