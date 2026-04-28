# Revit Version Compatibility

## Version Matrix

| Revit | Target framework | Revit API packages | Notes |
|-------|------------------|--------------------|-------|
| 2024 | `net48` | `Nice3point.Revit.Api.* 2024.*` | Last supported .NET Framework target in this repo. |
| 2025 | `net8.0-windows10.0.19041.0` | `Nice3point.Revit.Api.* 2025.*` | First .NET 8 Revit target. |
| 2026 | `net8.0-windows10.0.19041.0` | `Nice3point.Revit.Api.* 2026.*` | Uses .NET 8. |
| 2027 | `net10.0-windows10.0.19041.0` | `Nice3point.Revit.Api.* 2027.*` | Revit/Nice3point 2027 packages require .NET 10. |

`Directory.Build.props` owns the framework mapping via `$(RevitTFM)`. Do not let 2027 fall back to `net8.0` or `net48`; restore will fail with `NU1202` because Nice3point 2027 packages support `net10.0-windows7.0`.

## RevitMCPSDK 2027 Gap

As of the 2027 support work, NuGet has `RevitMCPSDK` packages only through `2026.*`; there is no `RevitMCPSDK 2027.*`.

The repo uses:

```xml
<RevitMCPSDKVersion Condition="'$(RevitVersion)' == '2027'">2026.*</RevitMCPSDKVersion>
<RevitMCPSDKVersion Condition="'$(RevitMCPSDKVersion)' == ''">$(RevitVersion).*</RevitMCPSDKVersion>
```

Project files should reference:

```xml
<PackageReference Include="RevitMCPSDK" Version="$(RevitMCPSDKVersion)" />
```

not:

```xml
<PackageReference Include="RevitMCPSDK" Version="$(RevitVersion).*" />
```

This is a compatibility fallback, not a guarantee that runtime behavior is fully validated in Revit 2027. Test inside Revit 2027 before shipping.

## 2027 Build Requirements

Revit 2027 requires a .NET 10 SDK on the build machine.

Check with:

```powershell
dotnet --list-sdks
```

If build fails with `NETSDK1045` or similar, install .NET 10 SDK and rerun.

## 2027 API Change: Curve.Intersect

Revit 2027 removes or changes the old detailed intersection overload:

```csharp
line1.Intersect(line2, out IntersectionResultArray results)
```

Use the newer API in `REVIT2027_OR_GREATER` code paths:

```csharp
var result = line1.Intersect(line2, CurveIntersectResultOption.Detailed);
if (result.Result == SetComparisonResult.Overlap)
{
    var overlaps = result.GetOverlaps();
    if (overlaps.Count > 0)
        return overlaps[0].Point;
}
```

Keep old versions on the legacy overload:

```csharp
var results = new IntersectionResultArray();
if (line1.Intersect(line2, out results) == SetComparisonResult.Overlap && results.Size > 0)
    return results.get_Item(0).XYZPoint;
```

Use `#if REVIT2027_OR_GREATER` for this split. `commandset/Utils/GeometryUtils.cs` is the known example.

## Build Commands

Preferred add-in payload check:

```powershell
.\scripts\Build-RevitVersions.ps1 -Versions 2027 -Configuration Release
```

Then build MSI:

```powershell
.\scripts\Build-OneMsi.ps1 -RevitVersion 2027 -ProductVersion 3.0.1 -Configuration Release
```

`Build-RevitVersions.ps1` should verify the actual payload exists under:

```text
plugin\bin\AddIn 2027 Release\
```

Do not trust a successful `dotnet build` alone; MSBuild can appear to finish while the expected add-in layout was not produced.
