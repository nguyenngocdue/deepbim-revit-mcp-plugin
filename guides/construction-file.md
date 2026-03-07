# Project Structure Reference

> Complete file-by-file breakdown of the `revit-mcp-plugin` project.

---

## Overview

```
revit-mcp-plugin/
├── revit-mcp-plugin.sln           # Visual Studio Solution (2 C# projects)
├── command.json                   # Command manifest (declares available commands)
│
├── plugin/                        # PROJECT 1: Revit Add-in (C#)
│   ├── RevitMCPPlugin.csproj
│   ├── revit-mcp-plugin.addin
│   ├── Core/
│   ├── Configuration/
│   └── Utils/
│
├── commandset/                    # PROJECT 2: Command implementations (C#)
│   ├── RevitMCPCommandSet.csproj
│   ├── Commands/
│   ├── Services/
│   └── Models/
│
└── server/                        # MCP Server (TypeScript)
    ├── package.json
    ├── tsconfig.json
    └── src/
        ├── index.ts
        ├── tools/
        └── utils/
```

---

## Root Files

| File | Purpose |
|------|---------|
| `revit-mcp-plugin.sln` | Visual Studio Solution containing `RevitMCPPlugin` and `RevitMCPCommandSet` projects. Configurations: Debug / Release, target Revit 2025. |
| `command.json` | Manifest declaring all available commands. Each entry maps a `commandName` to an `assemblyPath` (DLL). The plugin reads this to register commands. |

---

## Plugin (`plugin/`) — Revit Add-in

The plugin runs inside the Revit process. It creates a ribbon panel, listens for TCP connections on port 8080, and dispatches incoming JSON-RPC requests to the appropriate commands.

### Project File

| File | Purpose |
|------|---------|
| `RevitMCPPlugin.csproj` | C# Class Library targeting `net8.0-windows10.0.19041.0` (Revit 2025). Platform: x64. Uses WPF. NuGet packages: `Nice3point.Revit.Api.RevitAPI`, `Nice3point.Revit.Api.RevitAPIUI`, `RevitMCPSDK`, `Newtonsoft.Json`. Post-build target copies output to Revit Addins folder on Debug. |
| `revit-mcp-plugin.addin` | XML file Revit reads on startup to discover and load the plugin. Specifies the assembly path and entry point class (`revit_mcp_plugin.Core.Application`). |

### Core (`plugin/Core/`)

| File | Class | Role |
|------|-------|------|
| `Application.cs` | `Application : IExternalApplication` | **Entry point.** Called by Revit on startup. Creates a ribbon panel with "MCP Switch" toggle button and "Settings" button. On shutdown, stops the socket service. |
| `MCPServiceConnection.cs` | `MCPServiceConnection : IExternalCommand` | **Toggle button handler.** When clicked: if the server is running, stops it; otherwise initializes and starts it. Shows a TaskDialog with the current state. |
| `SocketService.cs` | `SocketService` (singleton) | **TCP server.** Listens on port 8080. Accepts client connections on background threads. Reads incoming bytes, decodes UTF-8, parses JSON-RPC, looks up the command in the registry, executes it, and writes back the JSON-RPC response. |
| `CommandExecutor.cs` | `CommandExecutor` | **Command dispatcher.** Takes a `JsonRPCRequest`, finds the matching `IRevitCommand` in the registry, calls `Execute()`, and wraps the result in a JSON-RPC success or error response. |
| `CommandManager.cs` | `CommandManager` | **Assembly loader.** Reads `commandRegistry.json`, resolves DLL paths, loads assemblies via `Assembly.LoadFrom()`, scans for types implementing `IRevitCommand`, creates instances (passing `UIApplication` to the constructor), and registers them in the command registry. |
| `RevitCommandRegistry.cs` | `RevitCommandRegistry : ICommandRegistry` | **Command lookup table.** A `Dictionary<string, IRevitCommand>` mapping command names to command instances. Provides `RegisterCommand()`, `TryGetCommand()`, `ClearCommands()`. |
| `ExternalEventManager.cs` | `ExternalEventManager` (singleton) | **ExternalEvent cache.** Creates and caches `ExternalEvent` instances keyed by handler name. Avoids recreating events on every request. Provides `GetOrCreateEvent()`. |

### Configuration (`plugin/Configuration/`)

| File | Class | Role |
|------|-------|------|
| `ConfigurationManager.cs` | `ConfigurationManager` | Loads `commandRegistry.json` from disk, deserializes it into `FrameworkConfig`. Provides the `Config` property to other components. |
| `FrameworkConfig.cs` | `FrameworkConfig` | Root configuration model. Contains `List<CommandConfig> Commands` and `ServiceSettings Settings`. |
| `CommandConfig.cs` | `CommandConfig` | Model for a single command entry: `CommandName`, `AssemblyPath`, `Enabled`, `SupportedRevitVersions`, `Description`. |
| `ServiceSettings.cs` | `ServiceSettings` | Global settings: `LogLevel` (default "Info"), `Port` (default 8080). |
| `DeveloperInfo.cs` | `DeveloperInfo` | Model for developer metadata: `Name`, `Email`, `Website`, `Organization`. |

### UI (`plugin/UI/`)

| File | Class | Role |
|------|-------|------|
| `SettingsWindow.xaml` | — | WPF Window layout: left navigation sidebar (ListBox) + right content area (Frame). 850×500px. |
| `SettingsWindow.xaml.cs` | `SettingsWindow : Window` | Code-behind. Creates `CommandSetSettingsPage`, navigates the Frame to it. Handles nav selection changes. |
| `CommandSetSettingsPage.xaml` | — | WPF Page layout: left panel lists available command sets, right panel shows a GridView (checkbox + name + description) for commands. Bottom toolbar: Open Folder, Refresh, Select All, Deselect All, Save. |
| `CommandSetSettingsPage.xaml.cs` | `CommandSetSettingsPage : Page` | Scans `Commands/` folder for subdirectories containing `command.json`. Detects supported Revit versions from year-named subfolders. Loads `commandRegistry.json` to restore enabled/disabled state. Save writes enabled commands back to `commandRegistry.json`. Also defines helper models: `CommandSetInfo`, `CommandRegistryModel`, `CommandJsonModel`, `CommandItemModel`. |

### Core — Additional (`plugin/Core/`)

| File | Class | Role |
|------|-------|------|
| `Settings.cs` | `Settings : IExternalCommand` | **Settings button handler.** Opens `SettingsWindow` as a non-modal window owned by the Revit main window. |

### Utils (`plugin/Utils/`)

| File | Class | Role |
|------|-------|------|
| `PathManager.cs` | `PathManager` (static) | Resolves filesystem paths: `GetAppDataDirectoryPath()` (plugin DLL location), `GetCommandsDirectoryPath()` (`Commands/` subfolder), `GetLogsDirectoryPath()`, `GetCommandRegistryFilePath()` (creates default file if missing). |
| `Logger.cs` | `Logger : ILogger` | Writes log entries to both `System.Diagnostics.Debug` and a daily log file (`Logs/mcp_YYYYMMDD.log`). Supports `Debug`, `Info`, `Warning`, `Error` levels. |

---

## CommandSet (`commandset/`) — Revit API Operations

The command set is a separate DLL loaded at runtime by the plugin. Each command consists of a **Command** class (parses input, raises ExternalEvent, waits for result) and an **EventHandler** class (runs on the Revit main thread, calls Revit API).

### Project File

| File | Purpose |
|------|---------|
| `RevitMCPCommandSet.csproj` | C# Class Library targeting `net8.0-windows10.0.19041.0`. Same NuGet packages as the plugin. Post-build target copies DLLs and `command.json` into the plugin's `Commands/RevitMCPCommandSet/2025/` folder, and into the Revit Addins folder on Debug. |

### Models (`commandset/Models/`)

| File | Class | Role |
|------|-------|------|
| `AIResult.cs` | `AIResult<T>` | Generic response wrapper: `bool Success`, `string Message`, `T Response`. Used by all commands to return results to the MCP server. |
| `ViewInfoResult.cs` | `ViewInfoResult` | DTO for view information: `Id`, `UniqueId`, `Name`, `ViewType`, `IsTemplate`, `Scale`, `DetailLevel`. |
| `ElementInfo.cs` | `ElementInfo` | DTO for element information: `Id`, `UniqueId`, `Name`, `Category`, `FamilyName`, `TypeName`, `Dictionary<string, string> Properties`. |

### Commands (`commandset/Commands/`)

Each command extends `ExternalEventCommandBase` from RevitMCPSDK.

| File | Class | CommandName | What it does |
|------|-------|-------------|--------------|
| `SayHelloCommand.cs` | `SayHelloCommand` | `say_hello` | Parses an optional `name` parameter, passes it to the handler, raises ExternalEvent, waits 10s. Returns greeting message. Connection test command. |
| `GetViewInfoCommand.cs` | `GetViewInfoCommand` | `get_view_info` | No parameters. Raises ExternalEvent, waits 10s. Returns `ViewInfoResult` with active view details. |
| `GetSelectedElementsCommand.cs` | `GetSelectedElementsCommand` | `get_selected_elements` | Parses optional `limit` parameter, passes to handler, raises ExternalEvent, waits 15s. Returns list of `ElementInfo` for selected elements. |

**Command pattern:**

```
Execute(JObject parameters, string requestId)
  1. Parse JSON parameters into C# objects
  2. Call handler.SetParameters(data)
  3. Call RaiseAndWaitForCompletion(timeoutMs)
     → ExternalEvent.Raise() puts handler in Revit's queue
     → ManualResetEvent.WaitOne() blocks until handler completes
  4. Return handler.Result
```

### Services (`commandset/Services/`)

Each event handler implements `IExternalEventHandler` + `IWaitableExternalEventHandler` from RevitMCPSDK.

| File | Class | Runs on Revit thread to... |
|------|-------|---------------------------|
| `SayHelloEventHandler.cs` | `SayHelloEventHandler` | Show a `TaskDialog` greeting. Returns `AIResult<string>` with the greeting text. |
| `GetViewInfoEventHandler.cs` | `GetViewInfoEventHandler` | Read `doc.ActiveView` properties (Id, Name, ViewType, Scale, DetailLevel). Returns `ViewInfoResult`. |
| `GetSelectedElementsEventHandler.cs` | `GetSelectedElementsEventHandler` | Read `uiDoc.Selection.GetElementIds()`, get element details (Name, Category, FamilyName, TypeName). Returns `AIResult<List<ElementInfo>>`. |

**Event handler pattern:**

```
Execute(UIApplication uiapp)
  try:
    1. Access Revit API (Document, Views, Elements...)
    2. Build result object
    3. Set Result property
  catch:
    4. Set error result
  finally:
    5. _resetEvent.Set()  ← unblocks the waiting Command
```

---

## MCP Server (`server/`) — TypeScript

The MCP server communicates with AI clients via stdio (MCP protocol) and forwards tool calls to the Revit plugin via TCP on port 8080 using JSON-RPC 2.0.

### Project Files

| File | Purpose |
|------|---------|
| `package.json` | npm package `revit-mcp-server`. Dependencies: `@modelcontextprotocol/sdk`, `zod`. Dev: `typescript`, `rimraf`. Scripts: `build` (tsc), `start`. |
| `tsconfig.json` | TypeScript config: ES2022 target, Node16 module resolution, strict mode, output to `build/`. |

### Entry Point

| File | Role |
|------|------|
| `src/index.ts` | Creates `McpServer` instance, calls `registerTools(server)` to load all tools, connects `StdioServerTransport`. This is what runs when the MCP server starts. |

### Tools (`server/src/tools/`)

| File | Function | Tool Name | Schema |
|------|----------|-----------|--------|
| `register.ts` | `registerTools()` | — | Scans the `tools/` directory, dynamically imports each file, finds and calls any function starting with `register`. New tools are auto-discovered. |
| `say_hello.ts` | `registerSayHelloTool()` | `say_hello` | `{ name?: string }` — Optional name to greet. |
| `get_view_info.ts` | `registerGetViewInfoTool()` | `get_view_info` | `{}` — No parameters. |
| `get_selected_elements.ts` | `registerGetSelectedElementsTool()` | `get_selected_elements` | `{ limit?: number }` — Optional max elements. |

**Tool pattern:**

```typescript
server.tool(name, description, zodSchema, async (args) => {
  const response = await withRevitConnection(client =>
    client.sendCommand(commandName, args)
  );
  return { content: [{ type: "text", text: JSON.stringify(response) }] };
});
```

### Utils (`server/src/utils/`)

| File | Class/Function | Role |
|------|---------------|------|
| `SocketClient.ts` | `RevitClientConnection` | TCP client that connects to `localhost:8080`. Sends JSON-RPC 2.0 requests (`sendCommand(method, params)`), buffers incoming data, resolves responses by matching request IDs. 2-minute timeout per command. |
| `ConnectionManager.ts` | `withRevitConnection()` | Mutex-based connection wrapper. Ensures only one TCP connection at a time (Revit processes commands sequentially). Creates connection, executes operation, disconnects, releases mutex. 5-second connection timeout. |

---

## Data Flow Summary

```
AI Client
  │ stdio (MCP protocol)
  ▼
MCP Server (TypeScript)
  │ tool handler → withRevitConnection() → sendCommand()
  │ TCP localhost:8080 (JSON-RPC 2.0)
  ▼
Revit Plugin (C#)
  │ SocketService → ProcessJsonRPCRequest() → registry.TryGetCommand()
  │ command.Execute() → ExternalEvent.Raise()
  ▼
CommandSet (C#)
  │ EventHandler.Execute(UIApplication) — runs on Revit main thread
  │ Calls Revit API → builds result → _resetEvent.Set()
  ▼
Revit API
```

---

## File Count

| Component | Files | Language |
|-----------|-------|----------|
| Plugin | 16 source + 2 config | C# |
| CommandSet | 9 source | C# |
| MCP Server | 8 source | TypeScript |
| Root | 2 | JSON / SLN |
| **Total** | **37 files** | |
