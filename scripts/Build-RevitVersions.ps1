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
$dotnetPath = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnetPath) {
    $dotnetCandidates = @(
        (Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'dotnet\dotnet.exe')
    )
    $dotnetPath = $dotnetCandidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
}
if (-not $dotnetPath) {
    throw "dotnet was not found on PATH or in the standard Program Files locations."
}

if (-not $SolutionPath) {
    $buildTargets = @(
        (Join-Path $rootDir 'commandset\RevitMCPCommandSet.csproj'),
        (Join-Path $rootDir 'plugin\RevitMCPPlugin.csproj')
    )
} else {
    $buildTargets = @($SolutionPath)
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
    $baseConfiguration = $Configuration
    if ($Configuration -match '^(Debug|Release)-\d{4}$') {
        $baseConfiguration = $Matches[1]
    }

    Write-Host "`n--- Revit $ver ---" -ForegroundColor Green
    foreach ($buildTarget in $buildTargets) {
        $buildArgs = @(
            'build', $buildTarget,
            '-c', $Configuration,
            "-p:RevitVersion=$ver",
            '--no-incremental'
        )
        Write-Host "$dotnetPath $($buildArgs -join ' ')" -ForegroundColor DarkGray
        $r = & $dotnetPath @buildArgs 2>&1
        $success = $?
        $exitCode = $LASTEXITCODE
        if (-not $success -or ($null -ne $exitCode -and $exitCode -ne 0)) {
            $r | Write-Host
            $displayExitCode = if ($null -ne $exitCode) { $exitCode } else { 1 }
            Write-Host "Build failed for Revit $ver (exit code $displayExitCode)." -ForegroundColor Red
            Write-Host "Target: $buildTarget" -ForegroundColor Red
            exit $displayExitCode
        }
    }

    $addinManifest = Join-Path $rootDir "plugin\bin\AddIn $ver $baseConfiguration\DeepBimRevitMCPlugin.addin"
    if (-not (Test-Path $addinManifest)) {
        $r | Write-Host
        Write-Host "Build completed but add-in payload was not generated for Revit $ver." -ForegroundColor Red
        Write-Host "Missing: $addinManifest" -ForegroundColor Red
        exit 1
    }

    Write-Host "Revit $ver OK." -ForegroundColor Green
}

Write-Host "`nAll builds completed." -ForegroundColor Cyan
