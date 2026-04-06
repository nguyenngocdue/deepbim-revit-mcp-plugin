# DeepBimMCPToolCommands

This project contains Revit tool commands organized with an MVVM-oriented structure.

Main flow:
`Command (bootstrapper) -> ViewModel -> Feature Service -> Execution Service -> Gateway -> Revit API / RevitMCPCommandSet`

Folder map:
- `Commands`: Revit `IExternalCommand` entry points.
- `ViewModels`: state and execution orchestration for each tool.
- `Models`: DTOs shared across ViewModels and Services.
- `Services`: reusable application and domain services.
- `Views`: output adapters (for example `TaskDialog` presenters).
