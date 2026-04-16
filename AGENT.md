# revit-mcp-plugin — Project Context for AI Agents

## What This Project Does

Bridges **AI clients (Claude, Cursor, Cline)** ↔ **Autodesk Revit** via the MCP protocol.
AI sends commands → MCP Server (TypeScript) → TCP JSON-RPC → Revit Plugin (C#) → CommandSet (C#) → Revit API.

---

## Architecture (3 Layers)

```
AI Client (Claude/Cursor/Cline)
    │ stdio (MCP Protocol)
    ▼
server/          ← TypeScript MCP Server  (Node.js)
    │ TCP localhost:8080 (JSON-RPC 2.0)
    ▼
plugin/          ← Revit Add-in (C#, runs inside Revit process)
    │ ExternalEvent (switch to Revit main thread)
    ▼
commandset/      ← Command implementations (C#, loaded as DLL at runtime)
    │ Revit API
    ▼
Autodesk Revit
```

---

## Project Structure

```
revit-mcp-plugin/
├── AGENT.md                       ← This file (AI context)
├── command.json                   ← Command manifest (declares all commands + DLL paths)
├── revit-mcp-plugin.sln           ← Visual Studio Solution (4 C# projects)
├── Directory.Build.props          ← Shared MSBuild props (Revit version targets)
├── RevitVersions.json             ← Supported Revit versions config
├── RevitVersion.generated.props   ← Auto-generated version props (by Generate-RevitVersionProps.ps1)
├── BUILD.md                       ← Build instructions
├── README.md                      ← Project overview
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
│   ├── GlobalUsings.cs            ← Global using directives
│   ├── Commands/                  ← IRevitCommand classes (parse input, raise ExternalEvent, wait for result)
│   │   ├── Access/                ← Read-only queries
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
│   │   ├── Common/                ← JZPoint (3D mm), JZLine, JZFace, FilterSetting, OperationSetting, etc.
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
│   │   ├── Base/
│   │   │   └── BaseToolCommand.cs ← Abstract base class for tool commands
│   │   ├── Geometry/
│   │   │   └── ExtractElementSurfacesCommand.cs
│   │   ├── Experimental/
│   │   └── Tests/
│   └── Services/
│       ├── Core/
│       └── Features/
│           └── Geometry/
│
├── DevToolV2Commands/             ← PROJECT 4: DevTool V2 (development/test commands)
│   ├── DevToolV2Commands.csproj
│   └── Commands/
│       └── TestSayHelloCommand.cs
│
├── installers/
│   └── msi/                       ← WiX MSI installer project
│
├── guides/
│   ├── GUIDE.md                   ← Full architecture deep-dive
│   ├── WORKFLOW.md                ← Step-by-step project creation guide
│   └── construction-file.md       ← File-by-file reference
│
├── images/                        ← Icons and images for Ribbon UI
└── scripts/
    ├── Build-RevitVersions.ps1        ← Builds for multiple Revit versions
    ├── Generate-RevitVersionProps.ps1 ← Generates RevitVersion.generated.props
    └── check-revit-mcp-connection.ps1 ← Test TCP connection to running Revit plugin
```

```
E:\C# Tool Revit\revit-mcp\revit-mcp-server\   ← MCP SERVER (TypeScript, separate folder)
    ├── package.json               ← pnpm, name: revit-mcp-server, main: build/index.js
    ├── tsconfig.json
    ├── Dockerfile                 ← Docker support for deployment
    ├── render.yaml                ← Render.com deployment config
    ├── .env                       ← Environment variables
    ├── src/
    │   ├── index.ts               ← Entry point: McpServer + StdioServerTransport + registerTools()
    │   ├── tools/                 ← One file per MCP tool (auto-registered via register.ts)
    │   │   ├── register.ts        ← Scans dir, imports each file, calls register*() function
    │   │   ├── get_current_view_info.ts
    │   │   ├── get_current_view_elements.ts
    │   │   ├── get_selected_elements.ts
    │   │   ├── get_available_family_types.ts
    │   │   ├── get_material_quantities.ts
    │   │   ├── get_sheet_exportable_properties.ts
    │   │   ├── create_line_based_element.ts
    │   │   ├── create_point_based_element.ts
    │   │   ├── create_surface_based_element.ts
    │   │   ├── create_grid.ts
    │   │   ├── create_level.ts
    │   │   ├── create_room.ts
    │   │   ├── create_dimensions.ts
    │   │   ├── create_structural_framing_system.ts
    │   │   ├── ai_element_filter.ts
    │   │   ├── analyze_model_statistics.ts
    │   │   ├── operate_element.ts
    │   │   ├── modify_element.ts
    │   │   ├── delete_element.ts
    │   │   ├── color_elements.ts
    │   │   ├── tag_all_walls.ts
    │   │   ├── tag_all_rooms.ts
    │   │   ├── export_room_data.ts
    │   │   ├── export_sheets_to_excel.ts
    │   │   ├── store_project_data.ts  ← Lưu data vào SQLite
    │   │   ├── store_room_data.ts
    │   │   ├── query_stored_data.ts   ← Query từ SQLite
    │   │   ├── search_modules.ts      ← Tìm kiếm modules
    │   │   ├── use_module.ts          ← Dùng module đã lưu
    │   │   ├── send_code_to_revit.ts  ← Gửi C# code để thực thi trong Revit
    │   │   ├── hello_world.ts
    │   │   └── say_hello.ts
    │   ├── utils/
    │   │   ├── ConnectionManager.ts   ← Mutex + TCP connection pool to Revit (localhost:8080)
    │   │   └── SocketClient.ts        ← JSON-RPC 2.0 client over TCP socket
    │   └── database/
    │       └── service.ts             ← better-sqlite3 service (store/query project & room data)
    ├── build/                         ← Compiled JS output (tsc → pnpm build)
    └── doc/
        ├── guide-to-build-server.md
        ├── guide-to-deploy-render.md
        └── huong-dan-trien-khai.md
```

---

## Key Patterns

### Adding a New Command (full flow)

1. **`commandset/Models/`** — Add request/response DTO if needed
2. **`commandset/Services/`** — Create `XxxEventHandler : IExternalEventHandler`
   - Runs on Revit main thread; calls Revit API here
   - Stores result in a shared field, then signals a `ManualResetEventSlim`
3. **`commandset/Commands/`** — Create `XxxCommand : IRevitCommand`
   - Deserializes `JObject params` → request DTO
   - Calls `ExternalEventManager.GetOrCreateEvent(handler)` then `.Raise()`
   - Waits on the `ManualResetEventSlim` with timeout
   - Returns `AIResult<T>` serialized as JSON string
4. **`command.json`** — Add entry: `{ "commandName": "xxx", "assemblyPath": "RevitMCPCommandSet/{VERSION}/RevitMCPCommandSet.dll", "enabled": true }`

### Why ExternalEvent?
Revit API can only be called from the **Revit main thread**. The TCP socket runs on a background thread → must use `IExternalEventHandler` + `ExternalEvent.Raise()` to marshal back to the main thread.

### JSON-RPC Flow
```
TCP request  →  SocketService  →  CommandExecutor.Execute(request)
             →  registry.TryGetCommand(method)  →  command.Execute(params)
             →  ExternalEvent.Raise()  →  EventHandler runs on main thread
             →  result returned  →  JSON-RPC response written to socket
```

### Coordinate System
All coordinates use **millimeters** in DTOs (`JZPoint.x/y/z`). The EventHandlers convert to Revit internal feet: `UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters)`.

---

## Build

```powershell
# Build for Revit 2025 (default)
dotnet build revit-mcp-plugin.sln -c Debug -p:RevitVersion=2025

# Output: plugin/bin/AddIn 2025 Debug/
```

Supported versions: 2020–2026 (set via `-p:RevitVersion=XXXX`).

---

## Available Commands (command.json)

| Command Name | Description |
|---|---|
| `get_current_view_info` | Current view metadata |
| `get_current_view_elements` | All elements in current view |
| `get_selected_elements` | Currently selected elements |
| `get_available_family_types` | Loaded family types |
| `create_line_based_element` | Walls, beams, pipes (line-based) |
| `create_point_based_element` | Furniture, columns (point-based) |
| `create_surface_based_element` | Floors, ceilings (surface-based) |
| `create_grid` | Grid system with spacing |
| `create_structural_framing_system` | Beam framing grid |
| `create_room` | Place rooms at locations |
| `create_level` | Levels at elevations |
| `create_dimensions` | Dimension annotations |
| `ai_element_filter` | Query elements by criteria |
| `operate_element` | Select / color / hide / isolate elements |
| `color_splash` | Color elements by parameter value |
| `tag_walls` | Tag all walls in view |
| `tag_rooms` | Tag all rooms in view |
| `delete_element` | Delete by ElementId |
| `export_room_data` | Room data with properties |
| `get_material_quantities` | Material takeoffs |
| `analyze_model_statistics` | Model complexity stats |
| `export_sheets_to_excel` | Sheet data → Excel |
| `get_sheet_exportable_properties` | Available sheet parameters |
| `send_code_to_revit` | Execute dynamic C# code in Revit |
| `say_hello` | Test greeting dialog |
