# Architecture — revit-mcp-plugin

## Plugin workspace (revit-mcp-plugin.sln — 4 C# projects)

```
revit-mcp-plugin/
├── command.json                   ← Command manifest (declares all commands + DLL paths)
├── revit-mcp-plugin.sln           ← Visual Studio Solution (4 C# projects)
├── Directory.Build.props          ← Shared MSBuild props (Revit version targets)
├── RevitVersions.json             ← Supported Revit versions config
├── RevitVersion.generated.props   ← Auto-generated version props (by Generate-RevitVersionProps.ps1)
│
├── plugin/                        ← PROJECT 1: Revit Add-in
│   ├── RevitMCPPlugin.csproj      ← net8.0-windows10.0.19041.0, x64, WPF
│   ├── DeepBimRevitMCPlugin.addin ← Revit addin manifest (entry point class + DLL path)
│   ├── Core/
│   │   ├── Application.cs         ← IExternalApplication entry point, creates Ribbon
│   │   ├── SocketService.cs       ← TCP listener on port 8080 (singleton)
│   │   ├── CommandExecutor.cs     ← Dispatches JSON-RPC to IRevitCommand
│   │   ├── CommandManager.cs      ← Loads DLLs, scans for IRevitCommand, registers them
│   │   ├── RevitCommandRegistry.cs← Dictionary<string, IRevitCommand>
│   │   ├── ExternalEventManager.cs← Caches ExternalEvent instances (avoid re-creation)
│   │   ├── MCPServiceConnection.cs← Toggle button to start/stop TCP server
│   │   ├── ExportSheetsToExcel.cs ← IExternalCommand for sheet export
│   │   └── RibbonIconHelper.cs    ← Loads icon images for Ribbon buttons
│   ├── Configuration/
│   │   ├── ConfigurationManager.cs← Loads commandRegistry.json
│   │   ├── FrameworkConfig.cs     ← Root config model
│   │   ├── CommandConfig.cs       ← Per-command config model
│   │   ├── ServiceSettings.cs     ← Port (default 8080), LogLevel
│   │   └── DeveloperInfo.cs       ← Developer metadata model
│   ├── UI/
│   │   ├── SettingsWindow.xaml(.cs)← WPF settings window (sidebar + Frame navigation)
│   │   └── CommandSetSettingsPage.xaml(.cs) ← Enable/disable commands UI
│   └── Utils/
│       ├── PathManager.cs         ← Resolves AppData, Commands/, Logs/ paths
│       └── Logger.cs              ← Writes to Debug output + daily log file
│
├── commandset/                    ← PROJECT 2: Command implementations
│   ├── RevitMCPCommandSet.csproj  ← Same target as plugin; post-build copies DLL to plugin
│   ├── GlobalUsings.cs
│   ├── Commands/                  ← IRevitCommand classes (parse input, raise ExternalEvent, wait for result)
│   │   ├── Access/
│   │   │   ├── GetSelectedElementsCommand.cs
│   │   │   ├── GetCurrentViewInfoCommand.cs
│   │   │   ├── GetCurrentViewElementsCommand.cs
│   │   │   └── GetAvailableFamilyTypesCommand.cs
│   │   ├── Architecture/
│   │   │   ├── CreateRoomCommand.cs
│   │   │   └── CreateLevelCommand.cs
│   │   ├── AnnotationComponents/
│   │   │   └── CreateDimensionCommand.cs
│   │   ├── DataExtraction/
│   │   │   ├── ExportRoomDataCommand.cs
│   │   │   ├── GetMaterialQuantitiesCommand.cs
│   │   │   └── AnalyzeModelStatisticsCommand.cs
│   │   ├── Delete/
│   │   │   └── DeleteElementCommand.cs
│   │   ├── ExecuteDynamicCode/
│   │   │   └── ExecuteCodeCommand.cs  ← send_code_to_revit: compiles + runs C# at runtime
│   │   ├── Test/
│   │   │   └── SayHelloCommand.cs
│   │   ├── AIElementFilterCommand.cs
│   │   ├── ColorSplashCommand.cs
│   │   ├── CreateGridCommand.cs
│   │   ├── CreateLineElementCommand.cs
│   │   ├── CreatePointElementCommand.cs
│   │   ├── CreateStructuralFramingSystemCommand.cs
│   │   ├── CreateSurfaceElementCommand.cs
│   │   ├── OperateElementCommand.cs
│   │   ├── TagRoomsCommand.cs
│   │   └── TagWallsCommand.cs
│   ├── Services/                  ← IExternalEventHandler classes (run on Revit main thread)
│   │   ├── Architecture/
│   │   │   ├── CreateRoomEventHandler.cs
│   │   │   └── CreateLevelEventHandler.cs
│   │   ├── AnnotationComponents/
│   │   │   └── CreateDimensionEventHandler.cs
│   │   ├── DataExtraction/
│   │   │   ├── ExportRoomDataEventHandler.cs
│   │   │   ├── GetMaterialQuantitiesEventHandler.cs
│   │   │   └── AnalyzeModelStatisticsEventHandler.cs
│   │   ├── AIElementFilterEventHandler.cs
│   │   ├── ColorSplashEventHandler.cs
│   │   ├── CreateGridEventHandler.cs
│   │   ├── CreateLineElementEventHandler.cs
│   │   ├── CreatePointElementEventHandler.cs
│   │   ├── CreateStructuralFramingSystemEventHandler.cs
│   │   ├── CreateSurfaceElementEventHandler.cs
│   │   ├── DeleteElementEventHandler.cs
│   │   ├── GetAvailableFamilyTypesEventHandler.cs
│   │   ├── GetCurrentViewElementsEventHandler.cs
│   │   ├── GetCurrentViewInfoEventHandler.cs
│   │   ├── GetSelectedElementsEventHandler.cs
│   │   ├── HelloWorldEventHandler.cs
│   │   ├── OperateElementEventHandler.cs
│   │   ├── SayHelloEventHandler.cs
│   │   ├── TagRoomsEventHandler.cs
│   │   └── TagWallsEventHandler.cs
│   ├── Models/                    ← DTOs shared between layers
│   │   ├── AIResult.cs            ← Generic result wrapper: Success, Message, Response<T>
│   │   ├── ElementInfo.cs         ← Element DTO: Id, Category, FamilyName, Properties dict
│   │   ├── ViewInfoResult.cs      ← View DTO: Id, Name, ViewType, Scale
│   │   ├── Common/                ← JZPoint (3D mm), JZLine, JZFace, FilterSetting, OperationSetting
│   │   ├── Architecture/
│   │   ├── Annotation/
│   │   ├── DataExtraction/
│   │   ├── MEP/
│   │   ├── Structure/
│   │   └── Views/
│   └── Utils/
│       ├── TransactionUtils.cs        ← Revit transaction wrappers
│       ├── GeometryUtils.cs           ← Coordinate/geometry helpers
│       ├── ElementIdExtensions.cs     ← Cross-version ElementId compatibility (R20–R26)
│       ├── JsonSchemaGenerator.cs     ← Generates JSON schema from C# types
│       ├── DeleteWarningSuperUtils.cs ← Suppress Revit delete warnings
│       └── HandleDuplicateTypeUtils.cs← Handle duplicate family type warnings
│
├── tools/                         ← PROJECT 3: DeepBimMCPToolCommands (advanced/experimental)
│   ├── DeepBimMCPToolCommands.csproj
│   ├── Commands/
│   │   ├── Base/BaseToolCommand.cs
│   │   ├── Geometry/ExtractElementSurfacesCommand.cs
│   │   ├── Experimental/
│   │   └── Tests/
│   └── Services/
│       ├── Core/
│       └── Features/Geometry/
│
├── DevToolV2Commands/             ← PROJECT 4: DevTool V2 (development/test commands)
│   ├── DevToolV2Commands.csproj
│   └── Commands/TestSayHelloCommand.cs
│
├── installers/msi/                ← WiX MSI installer project
├── images/                        ← Icons for Ribbon UI
└── scripts/
    ├── Build-RevitVersions.ps1
    ├── Generate-RevitVersionProps.ps1
    └── check-revit-mcp-connection.ps1
```
