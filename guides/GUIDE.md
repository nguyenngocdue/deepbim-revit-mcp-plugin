# Hướng dẫn chi tiết dự án mcp-servers-for-revit

> Tài liệu phân tích kiến trúc, luồng dữ liệu, source code và cách mở rộng dự án.

---

## Mục lục

- [1. Tổng quan dự án](#1-tổng-quan-dự-án)
- [2. Kiến trúc hệ thống](#2-kiến-trúc-hệ-thống)
- [3. Cấu trúc thư mục](#3-cấu-trúc-thư-mục)
- [4. Thành phần 1: MCP Server (TypeScript)](#4-thành-phần-1-mcp-server-typescript)
- [5. Thành phần 2: Revit Plugin (C#)](#5-thành-phần-2-revit-plugin-c)
- [6. Thành phần 3: CommandSet (C#)](#6-thành-phần-3-commandset-c)
- [7. Luồng dữ liệu chi tiết (End-to-End)](#7-luồng-dữ-liệu-chi-tiết-end-to-end)
- [8. Giao thức JSON-RPC 2.0](#8-giao-thức-json-rpc-20)
- [9. Cơ chế ExternalEvent (Revit Threading)](#9-cơ-chế-externalevent-revit-threading)
- [10. Danh sách tất cả Commands](#10-danh-sách-tất-cả-commands)
- [11. Models / DTOs](#11-models--dtos)
- [12. Hệ thống cấu hình](#12-hệ-thống-cấu-hình)
- [13. Hỗ trợ đa phiên bản Revit](#13-hỗ-trợ-đa-phiên-bản-revit)
- [14. Cách thêm Command mới](#14-cách-thêm-command-mới)
- [15. Ví dụ chi tiết: create_line_based_element](#15-ví-dụ-chi-tiết-create_line_based_element)
- [16. Testing](#16-testing)
- [17. Build và Release](#17-build-và-release)

---

## 1. Tổng quan dự án

**mcp-servers-for-revit** là một hệ thống cầu nối giữa **AI (Claude, Cline, Cursor...)** và **Autodesk Revit** thông qua giao thức **MCP (Model Context Protocol)**.

Dự án cho phép AI:
- **Đọc** thông tin mô hình Revit (view, elements, family types...)
- **Tạo** các đối tượng (tường, sàn, phòng, cột, dầm, ống gió...)
- **Chỉnh sửa** (tô màu, ẩn, cô lập, chọn elements...)
- **Xóa** elements
- **Trích xuất dữ liệu** (room data, material quantities, model statistics)
- **Thực thi code C# động** trực tiếp trong Revit

### Yêu cầu
- **Node.js 18+** (cho MCP server)
- **Autodesk Revit 2020 - 2026**

---

## 2. Kiến trúc hệ thống

```
┌─────────────────────────────────────┐
│     AI Client (Claude, Cline...)    │
│     Gửi yêu cầu qua MCP Protocol   │
└──────────────┬──────────────────────┘
               │ stdio (stdin/stdout)
               ▼
┌─────────────────────────────────────┐
│    MCP Server (Node.js/TypeScript)  │  ← thư mục server/
│    - 26 tools đăng ký với MCP SDK   │
│    - Validate input bằng Zod        │
│    - Chuyển lệnh qua TCP socket     │
└──────────────┬──────────────────────┘
               │ TCP localhost:8080 (JSON-RPC 2.0)
               ▼
┌─────────────────────────────────────┐
│    Revit Plugin (C# Add-in)        │  ← thư mục plugin/
│    - TCP listener trên port 8080    │
│    - Parse JSON-RPC request         │
│    - Tìm command trong Registry     │
│    - Dispatch command execution     │
└──────────────┬──────────────────────┘
               │ ExternalEvent (chuyển sang Revit thread)
               ▼
┌─────────────────────────────────────┐
│    CommandSet (C# Library)          │  ← thư mục commandset/
│    - 24 commands triển khai IRevitCommand
│    - EventHandlers chạy trên Revit thread
│    - Gọi Revit API tạo/đọc/sửa/xóa │
└──────────────┬──────────────────────┘
               │ Revit API
               ▼
┌─────────────────────────────────────┐
│         Autodesk Revit              │
└─────────────────────────────────────┘
```

### Tại sao cần 3 tầng?

| Tầng | Lý do |
|------|-------|
| **MCP Server (TS)** | MCP SDK chỉ hỗ trợ TypeScript/Python. Giao tiếp stdio với AI client. |
| **Plugin (C#)** | Chạy bên trong process Revit, truy cập `UIApplication`. Lắng nghe TCP. |
| **CommandSet (C#)** | Tách riêng logic nghiệp vụ, có thể thay thế/mở rộng dễ dàng. |

---

## 3. Cấu trúc thư mục

```
mcp-servers-for-revit/
├── mcp-servers-for-revit.sln      # Solution file (3 projects)
├── command.json                   # Manifest đăng ký commands
├── global.json                    # Config test runner
├── README.md                      # Hướng dẫn sử dụng
├── LICENSE                        # MIT License
│
├── server/                        # === MCP SERVER (TypeScript) ===
│   ├── package.json               # npm package: mcp-server-for-revit
│   ├── tsconfig.json              # TypeScript config
│   └── src/
│       ├── index.ts               # Entry point - khởi tạo McpServer
│       ├── tools/                 # Định nghĩa MCP tools
│       │   ├── register.ts        # Auto-load tất cả tool files
│       │   ├── create_line_based_element.ts
│       │   ├── create_point_based_element.ts
│       │   ├── create_surface_based_element.ts
│       │   ├── create_grid.ts
│       │   ├── create_level.ts
│       │   ├── create_room.ts
│       │   ├── create_dimensions.ts
│       │   ├── create_structural_framing_system.ts
│       │   ├── get_current_view_info.ts
│       │   ├── get_current_view_elements.ts
│       │   ├── get_available_family_types.ts
│       │   ├── get_selected_elements.ts
│       │   ├── get_material_quantities.ts
│       │   ├── ai_element_filter.ts
│       │   ├── analyze_model_statistics.ts
│       │   ├── operate_element.ts
│       │   ├── delete_element.ts
│       │   ├── color_elements.ts
│       │   ├── tag_all_walls.ts
│       │   ├── tag_all_rooms.ts
│       │   ├── export_room_data.ts
│       │   ├── store_project_data.ts
│       │   ├── store_room_data.ts
│       │   ├── query_stored_data.ts
│       │   ├── send_code_to_revit.ts
│       │   └── say_hello.ts
│       └── utils/
│           ├── ConnectionManager.ts  # Mutex + kết nối TCP đến Revit
│           └── SocketClient.ts       # JSON-RPC client qua TCP
│
├── plugin/                        # === REVIT PLUGIN (C#) ===
│   ├── RevitMCPPlugin.csproj      # Project file
│   ├── mcp-servers-for-revit.addin # Revit addin manifest
│   ├── Core/
│   │   ├── Application.cs         # IExternalApplication - entry point
│   │   ├── MCPServiceConnection.cs # Toggle bật/tắt server
│   │   ├── SocketService.cs       # TCP listener singleton
│   │   ├── CommandExecutor.cs     # Thực thi command từ JSON-RPC
│   │   ├── CommandManager.cs      # Load commands từ assemblies
│   │   ├── RevitCommandRegistry.cs # Registry lưu trữ commands
│   │   ├── ExternalEventManager.cs # Quản lý ExternalEvent lifecycle
│   │   └── Settings.cs            # Mở cửa sổ settings
│   ├── Configuration/
│   │   ├── ConfigurationManager.cs # Load config từ JSON
│   │   ├── CommandConfig.cs       # Model cho command config
│   │   ├── FrameworkConfig.cs     # Model cho toàn bộ config
│   │   ├── ServiceSettings.cs     # Model cho service settings
│   │   └── DeveloperInfo.cs       # Model cho developer info
│   ├── UI/
│   │   ├── SettingsWindow.xaml.cs  # WPF settings window
│   │   └── CommandSetSettingsPage.xaml.cs  # Trang settings command sets
│   ├── Utils/
│   │   ├── PathManager.cs         # Quản lý đường dẫn
│   │   └── Logger.cs              # Logger implementation
│   └── Properties/
│       └── AssemblyInfo.cs        # Assembly metadata
│
├── commandset/                    # === COMMAND SET (C#) ===
│   ├── RevitMCPCommandSet.csproj  # Project file
│   ├── Commands/                  # Command classes
│   │   ├── CreateLineElementCommand.cs
│   │   ├── CreatePointElementCommand.cs
│   │   ├── CreateSurfaceElementCommand.cs
│   │   ├── CreateGridCommand.cs
│   │   ├── CreateStructuralFramingSystemCommand.cs
│   │   ├── ColorSplashCommand.cs
│   │   ├── TagWallsCommand.cs
│   │   ├── TagRoomsCommand.cs
│   │   ├── OperateElementCommand.cs
│   │   ├── AIElementFilterCommand.cs
│   │   ├── Access/                # Commands đọc dữ liệu
│   │   │   ├── GetSelectedElementsCommand.cs
│   │   │   ├── GetCurrentViewInfoCommand.cs
│   │   │   ├── GetCurrentViewElementsCommand.cs
│   │   │   └── GetAvailableFamilyTypesCommand.cs
│   │   ├── Architecture/          # Commands kiến trúc
│   │   │   ├── CreateRoomCommand.cs
│   │   │   └── CreateLevelCommand.cs
│   │   ├── AnnotationComponents/  # Commands annotation
│   │   │   └── CreateDimensionCommand.cs
│   │   ├── DataExtraction/        # Commands trích xuất dữ liệu
│   │   │   ├── GetMaterialQuantitiesCommand.cs
│   │   │   ├── ExportRoomDataCommand.cs
│   │   │   └── AnalyzeModelStatisticsCommand.cs
│   │   ├── Delete/
│   │   │   └── DeleteElementCommand.cs
│   │   ├── ExecuteDynamicCode/
│   │   │   ├── ExecuteCodeCommand.cs
│   │   │   └── ExecuteCodeEventHandler.cs
│   │   └── Test/
│   │       └── SayHelloCommand.cs
│   │
│   ├── Services/                  # EventHandler classes
│   │   ├── CreateLineElementEventHandler.cs
│   │   ├── CreatePointElementEventHandler.cs
│   │   ├── CreateSurfaceElementEventHandler.cs
│   │   ├── CreateGridEventHandler.cs
│   │   ├── CreateStructuralFramingSystemEventHandler.cs
│   │   ├── ColorSplashEventHandler.cs
│   │   ├── TagWallsEventHandler.cs
│   │   ├── TagRoomsEventHandler.cs
│   │   ├── OperateElementEventHandler.cs
│   │   ├── AIElementFilterEventHandler.cs
│   │   ├── DeleteElementEventHandler.cs
│   │   ├── SayHelloEventHandler.cs
│   │   ├── GetSelectedElementsEventHandler.cs
│   │   ├── GetCurrentViewInfoEventHandler.cs
│   │   ├── GetCurrentViewElementsEventHandler.cs
│   │   ├── GetAvailableFamilyTypesEventHandler.cs
│   │   ├── Architecture/
│   │   │   ├── CreateRoomEventHandler.cs
│   │   │   └── CreateLevelEventHandler.cs
│   │   ├── AnnotationComponents/
│   │   │   └── CreateDimensionEventHandler.cs
│   │   └── DataExtraction/
│   │       ├── GetMaterialQuantitiesEventHandler.cs
│   │       ├── ExportRoomDataEventHandler.cs
│   │       └── AnalyzeModelStatisticsEventHandler.cs
│   │
│   ├── Models/                    # Data Transfer Objects
│   │   ├── Common/                # Shared models
│   │   │   ├── JZPoint.cs         # 3D point (mm)
│   │   │   ├── JZLine.cs          # 3D line segment
│   │   │   ├── JZFace.cs          # Face/surface
│   │   │   ├── AIResult.cs        # Generic result wrapper
│   │   │   ├── LineElement.cs     # Line-based element data
│   │   │   ├── PointElement.cs    # Point-based element data
│   │   │   ├── SurfaceElement.cs  # Surface-based element data
│   │   │   ├── ElementInfo.cs     # Element information
│   │   │   ├── ViewInfo.cs        # View information
│   │   │   ├── ViewElementsResult.cs
│   │   │   ├── FamilyTypeInfo.cs
│   │   │   ├── FilterSetting.cs
│   │   │   └── OperationSetting.cs
│   │   ├── Architecture/          # Architecture-specific models
│   │   ├── Annotation/            # Annotation models
│   │   ├── DataExtraction/        # Data extraction result models
│   │   ├── MEP/                   # MEP models
│   │   ├── Structure/             # Structure models
│   │   └── Views/                 # View-related models
│   │
│   └── Utils/                     # Utility classes
│       ├── TransactionUtils.cs    # Transaction wrappers
│       ├── ProjectUtils.cs        # Revit project helpers
│       ├── GeometryUtils.cs       # Geometry calculations
│       ├── ElementIdExtensions.cs # ElementId compatibility
│       ├── JsonSchemaGenerator.cs # JSON schema generation
│       ├── DeleteWarningSuperUtils.cs
│       └── HandleDuplicateTypeUtils.cs
│
├── tests/                         # === TESTS ===
│   └── commandset/
│       ├── AssemblyInfo.cs
│       ├── ColorSplashTests.cs
│       ├── TagRoomsTests.cs
│       ├── Architecture/
│       │   ├── CreateRoomTests.cs
│       │   └── CreateLevelTests.cs
│       └── DataExtraction/
│           ├── GetMaterialQuantitiesTests.cs
│           ├── ExportRoomDataTests.cs
│           └── AnalyzeModelStatisticsTests.cs
│
├── scripts/
│   └── release.ps1                # Version bump & release script
│
├── assets/                        # Images cho documentation
└── .github/                       # CI/CD workflows
```

---

## 4. Thành phần 1: MCP Server (TypeScript)

### 4.1 Entry Point (`server/src/index.ts`)

```typescript
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { registerTools } from "./tools/register.js";

const server = new McpServer({
  name: "mcp-server-for-revit",
  version: "1.0.0",
});

async function main() {
  await registerTools(server);
  const transport = new StdioServerTransport();
  await server.connect(transport);
  console.error("Revit MCP Server start success");
}

main().catch((error) => {
  console.error("Error starting Revit MCP Server:", error);
  process.exit(1);
});
```

**Giải thích:**
- Tạo `McpServer` instance với tên và version
- `registerTools()` đăng ký tất cả tools từ thư mục `tools/`
- `StdioServerTransport` giao tiếp với AI client qua stdin/stdout

### 4.2 Auto-Registration (`server/src/tools/register.ts`)

```typescript
export async function registerTools(server: McpServer) {
  const __filename = fileURLToPath(import.meta.url);
  const __dirname = path.dirname(__filename);
  const files = fs.readdirSync(__dirname);

  // Lọc tất cả file .ts/.js trừ index và register
  const toolFiles = files.filter(
    (file) =>
      (file.endsWith(".ts") || file.endsWith(".js")) &&
      file !== "index.ts" && file !== "index.js" &&
      file !== "register.ts" && file !== "register.js"
  );

  for (const file of toolFiles) {
    const importPath = `./${file.replace(/\.(ts|js)$/, ".js")}`;
    const module = await import(importPath);
    
    // Tìm function bắt đầu bằng "register" và gọi nó
    const registerFunctionName = Object.keys(module).find(
      (key) => key.startsWith("register") && typeof module[key] === "function"
    );
    if (registerFunctionName) {
      module[registerFunctionName](server);
    }
  }
}
```

**Cơ chế:** Quét thư mục `tools/`, import từng file, tìm function có tên bắt đầu bằng `register` và gọi nó. Nghĩa là chỉ cần thêm file mới vào `tools/` là tự động được đăng ký.

### 4.3 Ví dụ Tool Definition (`create_line_based_element.ts`)

```typescript
import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerCreateLineBasedElementTool(server: McpServer) {
  server.tool(
    "create_line_based_element",              // Tên tool
    "Create one or more line-based elements...", // Mô tả
    {                                           // Zod schema (validation)
      data: z.array(z.object({
        category: z.string().describe("Revit built-in category"),
        typeId: z.number().optional(),
        locationLine: z.object({
          p0: z.object({ x: z.number(), y: z.number(), z: z.number() }),
          p1: z.object({ x: z.number(), y: z.number(), z: z.number() }),
        }),
        thickness: z.number(),
        height: z.number(),
        baseLevel: z.number(),
        baseOffset: z.number(),
      }))
    },
    async (args, extra) => {                    // Handler
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("create_line_based_element", args);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        return { content: [{ type: "text", text: `Failed: ${error.message}` }] };
      }
    }
  );
}
```

**Pattern cho mỗi tool:**
1. Đăng ký với `server.tool(name, description, zodSchema, handler)`
2. Handler gọi `withRevitConnection()` để gửi lệnh đến Revit plugin
3. Trả về kết quả dạng `{ content: [{ type: "text", text: ... }] }`

### 4.4 Connection Manager (`server/src/utils/ConnectionManager.ts`)

```typescript
let connectionMutex: Promise<void> = Promise.resolve();

export async function withRevitConnection<T>(
  operation: (client: RevitClientConnection) => Promise<T>
): Promise<T> {
  // Mutex đảm bảo chỉ 1 kết nối tại 1 thời điểm
  const previousMutex = connectionMutex;
  let releaseMutex: () => void;
  connectionMutex = new Promise<void>((resolve) => { releaseMutex = resolve; });
  await previousMutex;

  const revitClient = new RevitClientConnection("localhost", 8080);
  try {
    // Kết nối TCP đến Revit plugin
    await new Promise<void>((resolve, reject) => {
      revitClient.socket.on("connect", () => resolve());
      revitClient.socket.on("error", () => reject());
      revitClient.connect();
      setTimeout(() => reject(new Error("Timeout")), 5000);
    });
    return await operation(revitClient);
  } finally {
    revitClient.disconnect();
    releaseMutex!();
  }
}
```

**Điểm quan trọng:** Sử dụng **mutex** để serialize tất cả kết nối - tránh race condition khi nhiều tool gọi song song.

### 4.5 Socket Client (`server/src/utils/SocketClient.ts`)

```typescript
export class RevitClientConnection {
  host: string;
  port: number;
  socket: net.Socket;
  responseCallbacks: Map<string, (response: string) => void> = new Map();
  buffer: string = "";

  // Gửi command theo JSON-RPC 2.0
  public sendCommand(command: string, params: any = {}): Promise<any> {
    return new Promise((resolve, reject) => {
      const requestId = this.generateRequestId();
      const commandObj = {
        jsonrpc: "2.0",
        method: command,
        params: params,
        id: requestId,
      };

      // Lưu callback để xử lý response
      this.responseCallbacks.set(requestId, (responseData) => {
        const response = JSON.parse(responseData);
        if (response.error) reject(new Error(response.error.message));
        else resolve(response.result);
      });

      this.socket.write(JSON.stringify(commandObj));

      // Timeout 2 phút
      setTimeout(() => {
        if (this.responseCallbacks.has(requestId)) {
          this.responseCallbacks.delete(requestId);
          reject(new Error(`Command timed out: ${command}`));
        }
      }, 120000);
    });
  }
}
```

---

## 5. Thành phần 2: Revit Plugin (C#)

### 5.1 Entry Point (`plugin/Core/Application.cs`)

```csharp
public class Application : IExternalApplication
{
    public Result OnStartup(UIControlledApplication application)
    {
        // Tạo ribbon panel trong Revit
        RibbonPanel mcpPanel = application.CreateRibbonPanel("Revit MCP Plugin");

        // Nút "Revit MCP Switch" - bật/tắt TCP server
        PushButtonData pushButtonData = new PushButtonData(
            "ID_EXCMD_TOGGLE_REVIT_MCP", "Revit MCP\r\n Switch",
            Assembly.GetExecutingAssembly().Location,
            "revit_mcp_plugin.Core.MCPServiceConnection");
        mcpPanel.AddItem(pushButtonData);

        // Nút "Settings" - mở cửa sổ settings
        PushButtonData mcp_settings_pushButtonData = new PushButtonData(
            "ID_EXCMD_MCP_SETTINGS", "Settings",
            Assembly.GetExecutingAssembly().Location,
            "revit_mcp_plugin.Core.Settings");
        mcpPanel.AddItem(mcp_settings_pushButtonData);

        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        // Dừng SocketService khi Revit đóng
        if (SocketService.Instance.IsRunning)
            SocketService.Instance.Stop();
        return Result.Succeeded;
    }
}
```

**Khi Revit khởi động:** Tạo 2 nút trong ribbon - "Switch" để bật/tắt MCP server và "Settings" để cấu hình.

### 5.2 SocketService (`plugin/Core/SocketService.cs`) - TCP Server

```csharp
public class SocketService
{
    private static SocketService _instance;        // Singleton
    private TcpListener _listener;
    private int _port = 8080;                      // Port cố định
    private ICommandRegistry _commandRegistry;
    private CommandExecutor _commandExecutor;

    // Khởi tạo: load commands, tạo executor
    public void Initialize(UIApplication uiApp)
    {
        ExternalEventManager.Instance.Initialize(uiApp, _logger);
        _commandExecutor = new CommandExecutor(_commandRegistry, _logger);

        ConfigurationManager configManager = new ConfigurationManager(_logger);
        configManager.LoadConfiguration();

        CommandManager commandManager = new CommandManager(
            _commandRegistry, _logger, configManager, _uiApp);
        commandManager.LoadCommands();
    }

    // Lắng nghe TCP connections
    private void ListenForClients()
    {
        while (_isRunning)
        {
            TcpClient client = _listener.AcceptTcpClient();
            Thread clientThread = new Thread(HandleClientCommunication);
            clientThread.Start(client);
        }
    }

    // Xử lý message từ MCP server
    private void HandleClientCommunication(object clientObj)
    {
        TcpClient tcpClient = (TcpClient)clientObj;
        NetworkStream stream = tcpClient.GetStream();
        // Đọc → Parse JSON-RPC → Execute → Gửi response
        string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
        string response = ProcessJsonRPCRequest(message);
        byte[] responseData = Encoding.UTF8.GetBytes(response);
        stream.Write(responseData, 0, responseData.Length);
    }

    // Parse và dispatch JSON-RPC request
    private string ProcessJsonRPCRequest(string requestJson)
    {
        JsonRPCRequest request = JsonConvert.DeserializeObject<JsonRPCRequest>(requestJson);

        // Tìm command trong registry
        if (!_commandRegistry.TryGetCommand(request.Method, out var command))
            return CreateErrorResponse(request.Id, "Method not found");

        // Thực thi command
        object result = command.Execute(request.GetParamsObject(), request.Id);
        return CreateSuccessResponse(request.Id, result);
    }
}
```

### 5.3 RevitCommandRegistry (`plugin/Core/RevitCommandRegistry.cs`)

```csharp
public class RevitCommandRegistry : ICommandRegistry
{
    private readonly Dictionary<string, IRevitCommand> _commands = new();

    public void RegisterCommand(IRevitCommand command)
    {
        _commands[command.CommandName] = command;
    }

    public bool TryGetCommand(string commandName, out IRevitCommand command)
    {
        return _commands.TryGetValue(commandName, out command);
    }
}
```

**Đơn giản:** Dictionary ánh xạ `commandName → IRevitCommand` instance.

### 5.4 CommandManager (`plugin/Core/CommandManager.cs`) - Load Commands

```csharp
public class CommandManager
{
    public void LoadCommands()
    {
        string currentVersion = _versionAdapter.GetRevitVersion();

        foreach (var commandConfig in _configManager.Config.Commands)
        {
            if (!commandConfig.Enabled) continue;

            // Kiểm tra version compatibility
            if (!_versionAdapter.IsVersionSupported(commandConfig.SupportedRevitVersions))
                continue;

            // Thay thế {VERSION} placeholder trong đường dẫn assembly
            // Ví dụ: "RevitMCPCommandSet/{VERSION}/RevitMCPCommandSet.dll"
            //       → "RevitMCPCommandSet/2025/RevitMCPCommandSet.dll"
            commandConfig.AssemblyPath = commandConfig.AssemblyPath
                .Replace("{VERSION}", currentVersion);

            LoadCommandFromAssembly(commandConfig);
        }
    }

    private void LoadCommandFromAssembly(CommandConfig config)
    {
        Assembly assembly = Assembly.LoadFrom(assemblyPath);

        foreach (Type type in assembly.GetTypes())
        {
            if (typeof(IRevitCommand).IsAssignableFrom(type) && !type.IsAbstract)
            {
                // Tạo instance với UIApplication constructor
                var constructor = type.GetConstructor(new[] { typeof(UIApplication) });
                IRevitCommand command = (IRevitCommand)constructor.Invoke(new[] { _uiApplication });

                if (command.CommandName == config.CommandName)
                {
                    _commandRegistry.RegisterCommand(command);
                    break;
                }
            }
        }
    }
}
```

**Cơ chế load:**
1. Đọc config từ `commandRegistry.json`
2. Với mỗi command: load assembly DLL bằng reflection
3. Tìm class implement `IRevitCommand` có `CommandName` khớp
4. Tạo instance và đăng ký vào registry

### 5.5 ExternalEventManager (`plugin/Core/ExternalEventManager.cs`)

```csharp
public class ExternalEventManager
{
    private Dictionary<string, ExternalEventWrapper> _events = new();

    // Tạo hoặc lấy ExternalEvent cho handler
    public ExternalEvent GetOrCreateEvent(IWaitableExternalEventHandler handler, string key)
    {
        if (_events.TryGetValue(key, out var wrapper) && wrapper.Handler == handler)
            return wrapper.Event;

        ExternalEvent externalEvent = ExternalEvent.Create(handler);
        _events[key] = new ExternalEventWrapper { Event = externalEvent, Handler = handler };
        return externalEvent;
    }
}
```

**Vai trò:** Cache `ExternalEvent` instances - tránh tạo lại mỗi lần gọi command.

---

## 6. Thành phần 3: CommandSet (C#)

### 6.1 Command Pattern

Mỗi command gồm 2 class:

```
┌─────────────────────────────┐     ┌──────────────────────────────┐
│  Command                    │     │  EventHandler                │
│  (IRevitCommand)            │────→│  (IExternalEventHandler +    │
│                             │     │   IWaitableExternalEventHandler)
│  - Parse JSON params        │     │                              │
│  - Set handler parameters   │     │  - Chạy trên Revit thread    │
│  - Raise ExternalEvent      │     │  - Gọi Revit API             │
│  - Chờ kết quả (timeout)    │     │  - Trả kết quả qua property  │
│  - Return result            │     │  - Signal completion          │
└─────────────────────────────┘     └──────────────────────────────┘
```

### 6.2 Ví dụ Command (`CreateLineElementCommand.cs`)

```csharp
public class CreateLineElementCommand : ExternalEventCommandBase
{
    private CreateLineElementEventHandler _handler =>
        (CreateLineElementEventHandler)Handler;

    public override string CommandName => "create_line_based_element";

    public CreateLineElementCommand(UIApplication uiApp)
        : base(new CreateLineElementEventHandler(), uiApp) { }

    public override object Execute(JObject parameters, string requestId)
    {
        // 1. Parse JSON parameters thành C# objects
        List<LineElement> data = parameters["data"].ToObject<List<LineElement>>();

        // 2. Truyền data cho handler
        _handler.SetParameters(data);

        // 3. Raise ExternalEvent và chờ hoàn thành (timeout 10s)
        if (RaiseAndWaitForCompletion(10000))
            return _handler.Result;
        else
            throw new TimeoutException("Timeout");
    }
}
```

### 6.3 Ví dụ EventHandler (`CreateLineElementEventHandler.cs`)

```csharp
public class CreateLineElementEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
{
    private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
    public List<LineElement> CreatedInfo { get; private set; }
    public AIResult<List<int>> Result { get; private set; }

    public void SetParameters(List<LineElement> data)
    {
        CreatedInfo = data;
        _resetEvent.Reset();
    }

    // Chạy trên Revit thread
    public void Execute(UIApplication uiapp)
    {
        try
        {
            var elementIds = new List<int>();

            foreach (var data in CreatedInfo)
            {
                // Xác định BuiltInCategory
                BuiltInCategory builtInCategory;
                Enum.TryParse(data.Category.Replace(".", ""), true, out builtInCategory);

                // Tìm Level gần nhất
                Level baseLevel = doc.FindNearestLevel(data.BaseLevel / 304.8);

                // Tìm FamilySymbol / WallType / DuctType
                // ... (logic tìm kiếm type)

                // Tạo element trong Transaction
                using (Transaction transaction = new Transaction(doc, "Create element"))
                {
                    transaction.Start();

                    switch (builtInCategory)
                    {
                        case BuiltInCategory.OST_Walls:
                            Wall wall = Wall.Create(doc,
                                JZLine.ToLine(data.LocationLine),
                                wallType.Id, baseLevel.Id,
                                data.Height / 304.8, baseOffset, false, false);
                            elementIds.Add(wall.Id.GetIntValue());
                            break;

                        case BuiltInCategory.OST_DuctCurves:
                            Duct duct = Duct.Create(doc, ...);
                            elementIds.Add(duct.Id.GetIntValue());
                            break;

                        default:
                            var instance = doc.CreateInstance(symbol, ...);
                            elementIds.Add(instance.Id.GetIntValue());
                            break;
                    }

                    transaction.Commit();
                }
            }

            Result = new AIResult<List<int>>
            {
                Success = true,
                Message = $"Successfully created {elementIds.Count} element(s).",
                Response = elementIds,
            };
        }
        catch (Exception ex)
        {
            Result = new AIResult<List<int>>
            {
                Success = false,
                Message = $"Error: {ex.Message}",
            };
        }
        finally
        {
            _resetEvent.Set(); // Báo hiệu hoàn thành
        }
    }

    public bool WaitForCompletion(int timeoutMilliseconds = 10000)
    {
        _resetEvent.Reset();
        return _resetEvent.WaitOne(timeoutMilliseconds);
    }
}
```

---

## 7. Luồng dữ liệu chi tiết (End-to-End)

Ví dụ: AI yêu cầu tạo 1 bức tường dài 5m, cao 3m.

```
Bước 1: AI → MCP Server
─────────────────────────
AI gọi tool "create_line_based_element" với params:
{
  "data": [{
    "category": "OST_Walls",
    "typeId": -1,
    "locationLine": {
      "p0": { "x": 0, "y": 0, "z": 0 },
      "p1": { "x": 5000, "y": 0, "z": 0 }
    },
    "thickness": 200,
    "height": 3000,
    "baseLevel": 0,
    "baseOffset": 0
  }]
}

Bước 2: MCP Server → Revit Plugin
──────────────────────────────────
MCP Server gửi JSON-RPC qua TCP:8080:
{
  "jsonrpc": "2.0",
  "method": "create_line_based_element",
  "params": { "data": [...] },
  "id": "1709827364abc123"
}

Bước 3: Plugin xử lý
─────────────────────
SocketService.ProcessJsonRPCRequest()
  → RevitCommandRegistry.TryGetCommand("create_line_based_element")
  → CreateLineElementCommand.Execute(params)

Bước 4: Command → Handler (ExternalEvent)
──────────────────────────────────────────
CreateLineElementCommand:
  → Parse params thành List<LineElement>
  → _handler.SetParameters(data)
  → ExternalEvent.Raise()  // Chuyển sang Revit thread
  → handler.WaitForCompletion(10000)  // Chờ tối đa 10s

Bước 5: Handler chạy trên Revit thread
───────────────────────────────────────
CreateLineElementEventHandler.Execute(UIApplication):
  → Tìm Level gần nhất (elevation 0)
  → Tìm WallType (default nếu typeId = -1)
  → Transaction.Start()
  → Wall.Create(doc, line, wallTypeId, levelId, height, offset, ...)
  → Transaction.Commit()
  → Result = AIResult { Success=true, Response=[wallId] }
  → _resetEvent.Set()  // Báo hoàn thành

Bước 6: Trả kết quả ngược lại
──────────────────────────────
Handler.Result → Command return → JSON-RPC response:
{
  "jsonrpc": "2.0",
  "id": "1709827364abc123",
  "result": {
    "Success": true,
    "Message": "Successfully created 1 element(s).",
    "Response": [12345]
  }
}
→ TCP response → MCP Server → AI nhận kết quả
```

---

## 8. Giao thức JSON-RPC 2.0

### Request format

```json
{
  "jsonrpc": "2.0",
  "method": "create_line_based_element",
  "params": {
    "data": [...]
  },
  "id": "unique-request-id"
}
```

### Success response

```json
{
  "jsonrpc": "2.0",
  "id": "unique-request-id",
  "result": {
    "Success": true,
    "Message": "...",
    "Response": [12345, 12346]
  }
}
```

### Error response

```json
{
  "jsonrpc": "2.0",
  "id": "unique-request-id",
  "error": {
    "code": -32603,
    "message": "Internal error: ..."
  }
}
```

### Error codes

| Code | Tên | Mô tả |
|------|-----|--------|
| -32700 | ParseError | JSON không hợp lệ |
| -32600 | InvalidRequest | Request format sai |
| -32601 | MethodNotFound | Command không tồn tại |
| -32602 | InvalidParams | Params không hợp lệ |
| -32603 | InternalError | Lỗi nội bộ |

---

## 9. Cơ chế ExternalEvent (Revit Threading)

### Vấn đề

Revit API chỉ cho phép gọi từ **main UI thread**. Nhưng TCP request đến từ **background thread**. Giải pháp: dùng `ExternalEvent`.

### Cách hoạt động

```
Background Thread (TCP)          Revit Main Thread
─────────────────────            ──────────────────
Command.Execute()
  │
  ├── handler.SetParameters(data)
  │
  ├── ExternalEvent.Raise()  ──→  Revit queue event
  │                                    │
  ├── ManualResetEvent.WaitOne()       │ (chờ Revit rảnh)
  │         (blocked)                  │
  │                                    ▼
  │                              handler.Execute(UIApplication)
  │                                    │
  │                              Gọi Revit API (an toàn)
  │                                    │
  │                              ManualResetEvent.Set()
  │                                    │
  ├── ← unblocked ────────────────────┘
  │
  └── return handler.Result
```

### Interfaces từ SDK

```csharp
// Command interface
public interface IRevitCommand
{
    string CommandName { get; }
    object Execute(JObject parameters, string requestId);
}

// Handler interface - mở rộng IExternalEventHandler
public interface IWaitableExternalEventHandler : IExternalEventHandler
{
    bool WaitForCompletion(int timeoutMilliseconds);
}
```

---

## 10. Danh sách tất cả Commands

### Truy vấn dữ liệu

| Command | MCP Tool | Mô tả |
|---------|----------|--------|
| `GetCurrentViewInfoCommand` | `get_current_view_info` | Lấy thông tin view hiện tại (tên, loại, tỉ lệ...) |
| `GetCurrentViewElementsCommand` | `get_current_view_elements` | Lấy danh sách elements trong view hiện tại |
| `GetSelectedElementsCommand` | `get_selected_elements` | Lấy elements đang được chọn |
| `GetAvailableFamilyTypesCommand` | `get_available_family_types` | Lấy tất cả family types có trong project |

### Tạo Elements

| Command | MCP Tool | Mô tả |
|---------|----------|--------|
| `CreatePointElementCommand` | `create_point_based_element` | Tạo elements dạng điểm (cửa, cửa sổ, nội thất) |
| `CreateLineElementCommand` | `create_line_based_element` | Tạo elements dạng tuyến (tường, dầm, ống gió) |
| `CreateSurfaceElementCommand` | `create_surface_based_element` | Tạo elements dạng mặt (sàn, trần) |
| `CreateGridCommand` | `create_grid` | Tạo hệ thống grid |
| `CreateLevelCommand` | `create_level` | Tạo levels tại các cao độ chỉ định |
| `CreateRoomCommand` | `create_room` | Tạo và đặt phòng |
| `CreateStructuralFramingSystemCommand` | `create_structural_framing_system` | Tạo hệ dầm kết cấu |
| `CreateDimensionCommand` | `create_dimensions` | Tạo kích thước annotation |

### Thao tác Elements

| Command | MCP Tool | Mô tả |
|---------|----------|--------|
| `OperateElementCommand` | `operate_element` | Chọn, tô màu, ẩn, cô lập elements |
| `DeleteElementCommand` | `delete_element` | Xóa elements theo ID |
| `ColorSplashCommand` | `color_elements` | Tô màu elements theo parameter |
| `AIElementFilterCommand` | `ai_element_filter` | Lọc elements theo tiêu chí |

### Annotation

| Command | MCP Tool | Mô tả |
|---------|----------|--------|
| `TagWallsCommand` | `tag_all_walls` | Tag tất cả tường trong view |
| `TagRoomsCommand` | `tag_all_rooms` | Tag tất cả phòng trong view |

### Trích xuất dữ liệu

| Command | MCP Tool | Mô tả |
|---------|----------|--------|
| `ExportRoomDataCommand` | `export_room_data` | Xuất dữ liệu phòng |
| `GetMaterialQuantitiesCommand` | `get_material_quantities` | Tính khối lượng vật liệu |
| `AnalyzeModelStatisticsCommand` | `analyze_model_statistics` | Phân tích thống kê mô hình |

### Khác

| Command | MCP Tool | Mô tả |
|---------|----------|--------|
| `ExecuteCodeCommand` | `send_code_to_revit` | Thực thi code C# động trong Revit |
| `SayHelloCommand` | `say_hello` | Hiển thị dialog chào (test kết nối) |

### Tools chỉ có ở MCP Server (không cần C# command)

| MCP Tool | Mô tả |
|----------|--------|
| `store_project_data` | Lưu metadata project vào SQLite local |
| `store_room_data` | Lưu metadata phòng vào SQLite local |
| `query_stored_data` | Truy vấn dữ liệu đã lưu |

---

## 11. Models / DTOs

### 11.1 Geometry Models

#### `JZPoint` - Điểm 3D (đơn vị mm)

```csharp
public class JZPoint
{
    [JsonProperty("x")] public double X { get; set; }
    [JsonProperty("y")] public double Y { get; set; }
    [JsonProperty("z")] public double Z { get; set; }

    // Chuyển đổi mm → ft (Revit dùng feet)
    public static XYZ ToXYZ(JZPoint jzPoint)
    {
        return new XYZ(jzPoint.X / 304.8, jzPoint.Y / 304.8, jzPoint.Z / 304.8);
    }
}
```

#### `JZLine` - Đoạn thẳng 3D

```csharp
public class JZLine
{
    [JsonProperty("p0")] public JZPoint P0 { get; set; }  // Điểm đầu
    [JsonProperty("p1")] public JZPoint P1 { get; set; }  // Điểm cuối

    public static Line ToLine(JZLine jzLine)
    {
        return Line.CreateBound(JZPoint.ToXYZ(jzLine.P0), JZPoint.ToXYZ(jzLine.P1));
    }
}
```

> **Quy ước đơn vị:** Tất cả input từ AI sử dụng **mm**. Khi chuyển sang Revit API phải chia cho **304.8** để ra **feet**.

### 11.2 Element Models

#### `LineElement` - Dữ liệu tạo element dạng tuyến

```csharp
public class LineElement
{
    [JsonProperty("category")]     public string Category { get; set; }    // VD: "OST_Walls"
    [JsonProperty("typeId")]       public int TypeId { get; set; }         // Family type ID
    [JsonProperty("locationLine")] public JZLine LocationLine { get; set; } // Đường định vị
    [JsonProperty("thickness")]    public double Thickness { get; set; }   // Độ dày (mm)
    [JsonProperty("height")]       public double Height { get; set; }      // Chiều cao (mm)
    [JsonProperty("baseLevel")]    public double BaseLevel { get; set; }   // Cao độ đáy (mm)
    [JsonProperty("baseOffset")]   public double BaseOffset { get; set; }  // Offset từ level (mm)
    [JsonProperty("parameters")]   public Dictionary<string, double> Parameters { get; set; }
}
```

#### `PointElement` - Dữ liệu tạo element dạng điểm

```csharp
public class PointElement
{
    public string Category { get; set; }
    public int TypeId { get; set; }
    public JZPoint LocationPoint { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double Rotation { get; set; }
    public double BaseLevel { get; set; }
    public double BaseOffset { get; set; }
}
```

#### `SurfaceElement` - Dữ liệu tạo element dạng mặt

```csharp
public class SurfaceElement
{
    public string Category { get; set; }
    public int TypeId { get; set; }
    public JZFace Boundary { get; set; }     // Biên dạng
    public double Thickness { get; set; }
    public double BaseLevel { get; set; }
    public double BaseOffset { get; set; }
}
```

### 11.3 Result Model

#### `AIResult<T>` - Kết quả trả về cho AI

```csharp
public class AIResult<T>
{
    public bool Success { get; set; }    // Thành công hay thất bại
    public string Message { get; set; }  // Thông báo chi tiết
    public T Response { get; set; }      // Dữ liệu trả về (generic)
}
```

Sử dụng:
- `AIResult<List<int>>` - trả về danh sách ElementId đã tạo
- `AIResult<ViewInfo>` - trả về thông tin view
- `AIResult<List<ElementInfo>>` - trả về danh sách elements

---

## 12. Hệ thống cấu hình

### 12.1 `command.json` - Manifest đăng ký commands

Đặt tại gốc mỗi command set. Khai báo tất cả commands có trong DLL.

```json
{
  "name": "RevitMCPCommandSet",
  "description": "Basic command collection for Revit AI assistance",
  "developer": {
    "name": "mcp-servers-for-revit",
    "organization": "mcp-servers-for-revit"
  },
  "commands": [
    {
      "commandName": "create_line_based_element",
      "description": "Create line based element such as wall",
      "assemblyPath": "RevitMCPCommandSet.dll"
    },
    ...
  ]
}
```

### 12.2 `commandRegistry.json` - Registry commands đã bật

Được tạo bởi Settings UI. Chứa danh sách commands đã bật và đường dẫn assembly theo version.

```json
{
  "commands": [
    {
      "commandName": "create_line_based_element",
      "assemblyPath": "RevitMCPCommandSet/{VERSION}/RevitMCPCommandSet.dll",
      "enabled": true,
      "supportedRevitVersions": [],
      "description": "Create line based element such as wall"
    }
  ],
  "settings": {
    "logLevel": "Info",
    "port": 8080
  }
}
```

Placeholder `{VERSION}` được thay bằng phiên bản Revit thực tế (VD: `2025`) khi load.

### 12.3 `CommandConfig.cs` - Model cấu hình

```csharp
public class CommandConfig
{
    [JsonProperty("commandName")]          public string CommandName { get; set; }
    [JsonProperty("assemblyPath")]         public string AssemblyPath { get; set; }
    [JsonProperty("enabled")]              public bool Enabled { get; set; } = true;
    [JsonProperty("supportedRevitVersions")] public string[] SupportedRevitVersions { get; set; }
    [JsonProperty("description")]          public string Description { get; set; }
}
```

### 12.4 Luồng cấu hình

```
command.json (per command set)
    │
    ▼
Settings UI (CommandSetSettingsPage)
    │ User bật/tắt commands
    ▼
commandRegistry.json (lưu trạng thái)
    │
    ▼
ConfigurationManager.LoadConfiguration()
    │
    ▼
CommandManager.LoadCommands()
    │ Reflection: Assembly.LoadFrom() → IRevitCommand
    ▼
RevitCommandRegistry (Dictionary<string, IRevitCommand>)
```

---

## 13. Hỗ trợ đa phiên bản Revit

### Target Frameworks

| Revit Version | .NET Target | Configuration |
|---------------|-------------|---------------|
| 2020 | net48 | Debug R20 / Release R20 |
| 2021 | net48 | Debug R21 / Release R21 |
| 2022 | net48 | Debug R22 / Release R22 |
| 2023 | net48 | Debug R23 / Release R23 |
| 2024 | net48 | Debug R24 / Release R24 |
| 2025 | net8.0-windows | Debug R25 / Release R25 |
| 2026 | net8.0-windows | Debug R26 / Release R26 |

### ElementId Compatibility

Revit 2024+ thay đổi `ElementId` từ `int` sang `long`. Extension methods xử lý tương thích:

```csharp
public static class ElementIdExtensions
{
    public static long GetValue(this ElementId elementId) { ... }
    public static int GetIntValue(this ElementId elementId) { ... }
}
```

### Assembly Layout khi deploy

```
Addins/2025/
├── mcp-servers-for-revit.addin
└── revit_mcp_plugin/
    ├── RevitMCPPlugin.dll
    └── Commands/
        └── RevitMCPCommandSet/
            ├── command.json
            └── 2025/
                ├── RevitMCPCommandSet.dll
                └── (dependencies...)
```

---

## 14. Cách thêm Command mới

### Bước 1: Tạo Model (nếu cần)

Tạo file trong `commandset/Models/`:

```csharp
// commandset/Models/Common/MyData.cs
public class MyData
{
    [JsonProperty("name")] public string Name { get; set; }
    [JsonProperty("value")] public double Value { get; set; }
}
```

### Bước 2: Tạo EventHandler

Tạo file trong `commandset/Services/`:

```csharp
// commandset/Services/MyCommandEventHandler.cs
public class MyCommandEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
{
    private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
    public MyData InputData { get; private set; }
    public AIResult<string> Result { get; private set; }

    public void SetParameters(MyData data)
    {
        InputData = data;
        _resetEvent.Reset();
    }

    public void Execute(UIApplication uiapp)
    {
        try
        {
            // Gọi Revit API ở đây
            Document doc = uiapp.ActiveUIDocument.Document;

            using (Transaction t = new Transaction(doc, "My Command"))
            {
                t.Start();
                // ... logic ...
                t.Commit();
            }

            Result = new AIResult<string> { Success = true, Message = "Done", Response = "OK" };
        }
        catch (Exception ex)
        {
            Result = new AIResult<string> { Success = false, Message = ex.Message };
        }
        finally
        {
            _resetEvent.Set();
        }
    }

    public bool WaitForCompletion(int timeoutMilliseconds = 10000)
    {
        _resetEvent.Reset();
        return _resetEvent.WaitOne(timeoutMilliseconds);
    }

    public string GetName() => "My Command";
}
```

### Bước 3: Tạo Command

Tạo file trong `commandset/Commands/`:

```csharp
// commandset/Commands/MyCommand.cs
public class MyCommand : ExternalEventCommandBase
{
    private MyCommandEventHandler _handler => (MyCommandEventHandler)Handler;

    public override string CommandName => "my_command";

    public MyCommand(UIApplication uiApp)
        : base(new MyCommandEventHandler(), uiApp) { }

    public override object Execute(JObject parameters, string requestId)
    {
        var data = parameters.ToObject<MyData>();
        _handler.SetParameters(data);

        if (RaiseAndWaitForCompletion(10000))
            return _handler.Result;
        throw new TimeoutException("Timeout");
    }
}
```

### Bước 4: Đăng ký trong `command.json`

```json
{
  "commandName": "my_command",
  "description": "Description of my command",
  "assemblyPath": "RevitMCPCommandSet.dll"
}
```

### Bước 5: Tạo MCP Tool

Tạo file trong `server/src/tools/`:

```typescript
// server/src/tools/my_command.ts
import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerMyCommandTool(server: McpServer) {
  server.tool(
    "my_command",
    "Description of my command for AI",
    {
      name: z.string().describe("Name parameter"),
      value: z.number().describe("Value parameter"),
    },
    async (args) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("my_command", args);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        return { content: [{ type: "text", text: `Failed: ${error.message}` }] };
      }
    }
  );
}
```

Tool sẽ được **tự động đăng ký** nhờ `register.ts`.

### Bước 6: Build và test

```bash
# Build C# (Visual Studio)
# Chọn configuration phù hợp (VD: Release R25)

# Build TypeScript
cd server
npm run build
```

---

## 15. Ví dụ chi tiết: create_line_based_element

### Toàn bộ luồng code

**1) AI gửi request tạo tường:**

```json
{
  "data": [{
    "category": "OST_Walls",
    "typeId": -1,
    "locationLine": {
      "p0": { "x": 0, "y": 0, "z": 0 },
      "p1": { "x": 5000, "y": 0, "z": 0 }
    },
    "thickness": 200,
    "height": 3000,
    "baseLevel": 0,
    "baseOffset": 0
  }]
}
```

**2) MCP Tool (`create_line_based_element.ts`):**
- Validate bằng Zod schema
- Gọi `withRevitConnection()` → `sendCommand("create_line_based_element", params)`
- Gửi JSON-RPC qua TCP:8080

**3) Plugin (`SocketService.cs`):**
- `ProcessJsonRPCRequest()` parse JSON-RPC
- `RevitCommandRegistry.TryGetCommand("create_line_based_element")` → `CreateLineElementCommand`
- `command.Execute(params, requestId)`

**4) Command (`CreateLineElementCommand.cs`):**
- `parameters["data"].ToObject<List<LineElement>>()` → parse thành C# object
- `_handler.SetParameters(data)` → truyền data cho handler
- `RaiseAndWaitForCompletion(10000)` → raise ExternalEvent, chờ 10s

**5) Handler (`CreateLineElementEventHandler.cs`):**
- Chạy trên Revit thread
- Parse category: `"OST_Walls"` → `BuiltInCategory.OST_Walls`
- Tìm level: `doc.FindNearestLevel(0 / 304.8)` → Level 0
- typeId = -1 → tìm WallType mặc định
- Convert: `JZLine.ToLine()` → `Line.CreateBound(XYZ(0,0,0), XYZ(16.4,0,0))` (5000mm = 16.4ft)
- `Wall.Create(doc, line, wallTypeId, levelId, 3000/304.8, 0, false, false)`
- `Result = AIResult { Success=true, Response=[wallId] }`
- `_resetEvent.Set()` → báo hoàn thành

**6) Trả kết quả:**
- Handler.Result → Command return → JSON-RPC success → TCP → MCP Server → AI

---

## 16. Testing

### Framework
- **Nice3point.TUnit.Revit** - chạy test trực tiếp trong Revit process
- Yêu cầu: .NET 10 SDK, Revit 2025/2026

### Chạy test

```bash
# Mở Revit trước, sau đó:
dotnet test -c Debug.R26 -r win-x64 tests/commandset

# Hoặc:
cd tests/commandset
dotnet run -c Debug.R26
```

### Cấu trúc test

```csharp
public class CreateLevelTests : RevitApiTest
{
    private static Document _doc;

    [Before(HookType.Class)]
    [HookExecutor<RevitThreadExecutor>]
    public static void Setup()
    {
        _doc = Application.NewProjectDocument(UnitSystem.Imperial);
    }

    [After(HookType.Class)]
    [HookExecutor<RevitThreadExecutor>]
    public static void Cleanup()
    {
        _doc?.Close(false);
    }

    [Test]
    public async Task CreateLevel_ValidElevation_Success()
    {
        // ... test logic ...
        await Assert.That(result).IsNotNull();
    }
}
```

---

## 17. Build và Release

### Build MCP Server

```bash
cd server
npm install
npm run build
# Output: server/build/
```

### Build C# Projects

Mở `mcp-servers-for-revit.sln` trong Visual Studio:
- Chọn configuration: `Release R25` (cho Revit 2025)
- Build Solution
- Output tại: `plugin/bin/AddIn 2025 Release/`

### Release

```powershell
# Bump version (cập nhật package.json + AssemblyInfo.cs, commit + tag)
./scripts/release.ps1 -Version 1.2.0

# Push tag để trigger CI/CD
git push origin main --tags
```

CI/CD tự động:
- Build cho tất cả versions Revit 2020-2026
- Tạo GitHub Release với ZIP files
- Publish npm package `mcp-server-for-revit`

---

## Phụ lục: Tóm tắt số liệu

| Metric | Giá trị |
|--------|---------|
| Tổng file C# | ~121 |
| Tổng file TypeScript | ~30 |
| Commands (C#) | 24 |
| MCP Tools | 26 |
| Revit versions hỗ trợ | 2020-2026 |
| Giao thức | JSON-RPC 2.0 qua TCP:8080 |
| MCP transport | stdio |
| SDK dependency | RevitMCPSDK (NuGet) |
| Đơn vị input | mm |
| Đơn vị Revit API | feet (1ft = 304.8mm) |
