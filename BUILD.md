# Building DeepBim-MCP (Revit add-in + MCP server + MSI)

How to build the .NET solution, the Node MCP server project, and the MSI installer. Matches the current pipeline in this repository.

## Prerequisites

| Component | Notes |
|-----------|--------|
| **Windows** | Required when building the **MSI** for Revit end users: `better-sqlite3` is native; running `npm ci` on Linux/WSL produces the wrong OS binaries. |
| **.NET SDK** | For `revit-mcp-plugin.sln` (plugin, commandset, tools, WiX). Version follows `Directory.Build.props` / Revit targets. |
| **Node.js + npm** | For `server/` (MCP server). `package-lock.json` is required for MSI builds. |
| **WiX Toolset** | Via NuGet: `installers/msi/DeepBimMCP.Installer.wixproj` uses `WixToolset.Sdk` (`dotnet build` is enough). |

## Quick build (add-in + server only, no MSI)

### 1. Revit solution (.NET)

From the repository root:

```powershell
dotnet build revit-mcp-plugin.sln -c Release -p:RevitVersion=2025
```

- Set `RevitVersion` to your installed Revit year (align with `RevitVersions.json` if needed).
- Add-in layout output is under `plugin\bin\AddIn {RevitVersion} Release\` (see `RevitMCPPlugin.csproj`, `AssembleAddIn` target).

### 2. MCP server (Node / TypeScript)

```powershell
cd server
npm ci
npm run build
```

- Produces `server/build/` (JavaScript from `tsc`).
- `npm run build` needs **devDependencies** (`typescript`). Do **not** use `npm ci --omit=dev` before this step.

Run the server locally:

```powershell
node build/index.js
```

## Building the MSI (installer)

The script handles **npm** (full install → TypeScript build → prune to production-only deps), generates WiX fragments for `server/build` and `server/node_modules`, then builds the add-in and the MSI.

Run **PowerShell** from the repository root:

```powershell
.\installers\msi\Build-Installer.ps1
```

Common options:

| Goal | Command |
|------|---------|
| Single Revit year | `.\installers\msi\Build-Installer.ps1 -Versions 2025` |
| Multiple years | `.\installers\msi\Build-Installer.ps1 -Versions 2024,2025` |
| All years in `RevitVersions.json` | `.\installers\msi\Build-Installer.ps1` |
| Custom MSI product version | `.\installers\msi\Build-Installer.ps1 -Versions 2025 -ProductVersion 1.2.0` |
| Reuse plugin output; rebuild MSI only | `.\installers\msi\Build-Installer.ps1 -Versions 2025 -SkipBuild` |

- MSIs are copied to `installers\msi\output\` (names like `deepbim-mcp-revit{year}-v{version}.msi`).

### What `Build-Installer.ps1` does (summary)

1. **`[0]`** Prepare MSI UI bitmaps, Node server, and WiX fragments:  
   - `Prepare-InstallerBranding.ps1` writes WiX-sized PNGs (**gitignored** `installers/msi/branding/`): banner uses `deepbim-logo-56.png` (else main logo) **right-aligned** with the left ~275px left white so WiX title text is not covered; dialog prefers `deepbim-logo-512.png` else `deepbim-logo.png`, centered in 493×312.  
   - In `server/`: `npm ci` → `npm run build` → `npm prune --omit=dev` (full `npm ci` is required for `tsc`; after `prune`, only runtime deps ship in the MSI).  
   - `Generate-ServerNodeModules.ps1` → `ServerNodeModules.wxs`; `Generate-ServerFiles.ps1` → `ServerFiles.wxs` (both gitignored).

2. **`[1]`** `dotnet build` the solution with `-p:RevitVersion=...`

3. **`[2]`** `dotnet build` `DeepBimMCP.Installer.wixproj` → MSI

### WiX / XML notes

- In `.wixproj` and in generated `.wxs` header comments, do **not** put the substring `--` inside XML comments `<!-- ... -->` (e.g. avoid documenting `npm ci --omit=dev` inside comments).

## Installed layout after MSI (reference)

See `installers/structure-folder.md`. In short: add-in under `%APPDATA%\Autodesk\Revit\Addins\{year}\`, folder `DeepBimRevitMCPlugin\server\` contains `index.js`, the compiled layout under `build`, and production **`node_modules`** so Node can resolve `@modelcontextprotocol/sdk`, `zod`, `better-sqlite3`, etc.

## Building the MSI with `dotnet` only (no script)

Not recommended: you must already have run `Prepare-InstallerBranding.ps1`, `npm ci` / build / prune in `server/`, and generated `ServerFiles.wxs` plus `ServerNodeModules.wxs`. Otherwise the WiX build fails.

## See also

- Short MSI command list: `build-command.md`
- Default Revit version / list: `RevitVersions.json`
