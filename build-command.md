# Build MSI Steps

## Build Revit 2026 v3.0.0

```powershell
# 1. Open PowerShell in the project folder
cd "E:\C# Tool Revit\revit-mcp\mcp-addin\revit-mcp-plugin"

# 2. Build the MSI
.\scripts\Build-OneMsi.ps1 -RevitVersion 2026 -ProductVersion 3.0.0

# 3. Verify the MSI output
Test-Path ".\installers\msi\output\DeepBimMCP-Revit2026-v3.0.0.msi"
```

## Change Version

```powershell
# Revit 2024
.\scripts\Build-OneMsi.ps1 -RevitVersion 2024 -ProductVersion 3.0.0

# Revit 2025
.\scripts\Build-OneMsi.ps1 -RevitVersion 2025 -ProductVersion 3.0.0

# Revit 2026
.\scripts\Build-OneMsi.ps1 -RevitVersion 2026 -ProductVersion 3.0.0
```

## Rebuild MSI Only

Use this only when the add-in payload already exists.

```powershell
.\scripts\Build-OneMsi.ps1 -RevitVersion 2026 -ProductVersion 3.0.0 -SkipPluginBuild
```
