# Workflow: Tạo dự án Revit MCP Plugin từ đầu

> Hướng dẫn từng bước tạo dự án giống `mcp-servers-for-revit` bằng Visual Studio.
> Giả sử bạn đang target **Revit 2025** (net8.0).

---

## Mục lục

- [Phần A: Tổng quan - Bạn cần tạo những gì?](#phần-a-tổng-quan---bạn-cần-tạo-những-gì)
- [Phần B: Tạo Solution trong Visual Studio](#phần-b-tạo-solution-trong-visual-studio)
- [Phần C: Tạo Project 1 - Plugin (Revit Add-in)](#phần-c-tạo-project-1---plugin-revit-add-in)
- [Phần D: Tạo Project 2 - CommandSet](#phần-d-tạo-project-2---commandset)
- [Phần E: Tạo MCP Server (TypeScript)](#phần-e-tạo-mcp-server-typescript)
- [Phần F: Kết nối tất cả lại - Workflow hoàn chỉnh](#phần-f-kết-nối-tất-cả-lại---workflow-hoàn-chỉnh)
- [Phần G: Build, Deploy và Debug](#phần-g-build-deploy-và-debug)
- [Phần H: Sơ đồ tổng hợp các bước](#phần-h-sơ-đồ-tổng-hợp-các-bước)

---

## Phần A: Tổng quan - Bạn cần tạo những gì?

Dự án gồm **3 thành phần**, tạo theo thứ tự:

```
Bước 1: Plugin (C#)      → Chạy trong Revit, lắng nghe TCP
Bước 2: CommandSet (C#)   → Thực thi Revit API
Bước 3: MCP Server (TS)   → Cầu nối AI ↔ Plugin
```

Cấu trúc cuối cùng:

```
my-revit-mcp/
├── my-revit-mcp.sln            ← Solution chứa 2 C# projects
├── command.json                ← Khai báo commands
├── plugin/                     ← Project 1: Revit Add-in
│   ├── MyRevitPlugin.csproj
│   ├── my-plugin.addin
│   └── Core/
│       ├── Application.cs
│       ├── SocketService.cs
│       ├── CommandExecutor.cs
│       ├── CommandManager.cs
│       └── ...
├── commandset/                 ← Project 2: Commands
│   ├── MyCommandSet.csproj
│   ├── Commands/
│   ├── Services/
│   └── Models/
└── server/                     ← MCP Server (TypeScript)
    ├── package.json
    └── src/
        ├── index.ts
        ├── tools/
        └── utils/
```

---

## Phần B: Tạo Solution trong Visual Studio

### B1. Mở Visual Studio → Create a new project

```
File → New → Project → Blank Solution
```

- Solution name: `my-revit-mcp`
- Location: chọn thư mục bạn muốn

### B2. Kết quả

```
my-revit-mcp/
└── my-revit-mcp.sln    ← Solution rỗng
```

---

## Phần C: Tạo Project 1 - Plugin (Revit Add-in)

### C1. Add Project vào Solution

```
Chuột phải Solution → Add → New Project
→ Chọn "Class Library" (C#)
→ Project name: MyRevitPlugin
→ Location: đặt trong thư mục "plugin" của solution
→ Framework: .NET 8.0 (hoặc .NET Framework 4.8 cho Revit ≤2024)
```

### C2. Chỉnh sửa .csproj

Đây là phần quan trọng nhất. Mở `MyRevitPlugin.csproj` và thay nội dung:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <OutputType>Library</OutputType>
        <RootNamespace>my_revit_plugin</RootNamespace>
        <UseWPF>true</UseWPF>
        <PlatformTarget>x64</PlatformTarget>

        <!-- Target Revit 2025 = net8.0 -->
        <TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
        <EnableDynamicLoading>true</EnableDynamicLoading>
    </PropertyGroup>

    <!-- NuGet Packages -->
    <ItemGroup>
        <!-- Revit API references (không cần cài Revit SDK thủ công) -->
        <PackageReference Include="Nice3point.Revit.Api.RevitAPI" Version="2025.*" />
        <PackageReference Include="Nice3point.Revit.Api.RevitAPIUI" Version="2025.*" />

        <!-- MCP SDK cho Revit (interfaces IRevitCommand, etc.) -->
        <PackageReference Include="RevitMCPSDK" Version="2025.*" />

        <!-- JSON serialization -->
        <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
    </ItemGroup>

    <!-- Copy .addin file vào output -->
    <ItemGroup>
        <None Update="my-plugin.addin">
            <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
        </None>
    </ItemGroup>

    <!-- Post-build: Copy vào Revit Addins folder (Debug only) -->
    <Target Name="CopyToRevit" AfterTargets="CoreBuild"
            Condition="$(Configuration.Contains('Debug'))">
        <ItemGroup>
            <AddinFile Include="$(ProjectDir)*.addin" />
            <OutputFiles Include="$(TargetDir)*.dll;$(TargetDir)*.pdb" />
        </ItemGroup>
        <PropertyGroup>
            <RevitAddinsDir>$(AppData)\Autodesk\Revit\Addins\2025\</RevitAddinsDir>
        </PropertyGroup>
        <Copy SourceFiles="@(AddinFile)" DestinationFolder="$(RevitAddinsDir)" />
        <Copy SourceFiles="@(OutputFiles)"
              DestinationFolder="$(RevitAddinsDir)my_revit_plugin\" />
    </Target>
</Project>
```

**Giải thích từng phần:**

| Phần | Tại sao cần? |
|------|-------------|
| `UseWPF` | Plugin dùng WPF cho Settings UI |
| `PlatformTarget=x64` | Revit chỉ chạy 64-bit |
| `net8.0-windows` | Revit 2025+ dùng .NET 8 |
| `EnableDynamicLoading` | Cho phép load assembly động |
| `Nice3point.Revit.Api.*` | Thay thế việc reference RevitAPI.dll thủ công |
| `RevitMCPSDK` | Cung cấp interfaces: `IRevitCommand`, `ExternalEventCommandBase`, etc. |
| `CopyToRevit` target | Tự động deploy khi build Debug |

### C3. Tạo file `.addin` - Revit đọc file này để load plugin

Tạo `plugin/my-plugin.addin`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>my-revit-mcp</Name>
    <Assembly>my_revit_plugin/MyRevitPlugin.dll</Assembly>
    <FullClassName>my_revit_plugin.Core.Application</FullClassName>
    <ClientId>YOUR-GUID-HERE</ClientId>
    <VendorId>your-vendor-id</VendorId>
    <VendorDescription>Your description</VendorDescription>
  </AddIn>
</RevitAddIns>
```

> Tạo GUID mới: Visual Studio → Tools → Create GUID → Registry Format

### C4. Tạo cấu trúc thư mục

```
plugin/
├── MyRevitPlugin.csproj
├── my-plugin.addin
├── Core/                    ← Logic chính
│   ├── Application.cs       ← Entry point (IExternalApplication)
│   ├── MCPServiceConnection.cs  ← Nút bật/tắt server
│   ├── SocketService.cs     ← TCP listener
│   ├── CommandExecutor.cs   ← Thực thi command
│   ├── CommandManager.cs    ← Load commands từ DLL
│   ├── RevitCommandRegistry.cs  ← Registry lưu commands
│   └── ExternalEventManager.cs  ← Quản lý ExternalEvent
├── Configuration/           ← Cấu hình
│   ├── ConfigurationManager.cs
│   ├── FrameworkConfig.cs
│   ├── CommandConfig.cs
│   └── ServiceSettings.cs
└── Utils/                   ← Tiện ích
    ├── PathManager.cs
    └── Logger.cs
```

### C5. Viết code - Theo thứ tự từ dưới lên

#### Bước C5.1: Utils (nền tảng)

**`PathManager.cs`** - Quản lý đường dẫn:

```csharp
namespace my_revit_plugin.Utils
{
    public static class PathManager
    {
        // Thư mục chứa plugin DLL
        public static string GetAppDataDirectoryPath()
        {
            string appPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            return Path.GetDirectoryName(appPath);
        }

        // Thư mục Commands/ chứa command set DLLs
        public static string GetCommandsDirectoryPath()
        {
            string dir = Path.Combine(GetAppDataDirectoryPath(), "Commands");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        // File commandRegistry.json
        public static string GetCommandRegistryFilePath()
        {
            string dir = GetCommandsDirectoryPath();
            string path = Path.Combine(dir, "commandRegistry.json");
            if (!File.Exists(path))
            {
                // Tạo file mặc định
                File.WriteAllText(path, "{\"commands\":[]}");
            }
            return path;
        }
    }
}
```

**`Logger.cs`** - Ghi log:

```csharp
namespace my_revit_plugin.Utils
{
    public class Logger : ILogger  // ILogger từ RevitMCPSDK
    {
        private readonly string _logFilePath;

        public Logger()
        {
            string logDir = Path.Combine(PathManager.GetAppDataDirectoryPath(), "Logs");
            if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
            _logFilePath = Path.Combine(logDir, $"mcp_{DateTime.Now:yyyyMMdd}.log");
        }

        public void Log(LogLevel level, string message, params object[] args)
        {
            string formatted = args.Length > 0 ? string.Format(message, args) : message;
            string entry = $"{DateTime.Now:HH:mm:ss} [{level}] {formatted}";
            System.Diagnostics.Debug.WriteLine(entry);
            try { File.AppendAllText(_logFilePath, entry + "\n"); } catch { }
        }

        public void Info(string msg, params object[] args) => Log(LogLevel.Info, msg, args);
        public void Error(string msg, params object[] args) => Log(LogLevel.Error, msg, args);
        public void Warning(string msg, params object[] args) => Log(LogLevel.Warning, msg, args);
        public void Debug(string msg, params object[] args) => Log(LogLevel.Debug, msg, args);
    }
}
```

#### Bước C5.2: Configuration (cấu hình)

**`CommandConfig.cs`:**

```csharp
namespace my_revit_plugin.Configuration
{
    public class CommandConfig
    {
        [JsonProperty("commandName")]  public string CommandName { get; set; }
        [JsonProperty("assemblyPath")] public string AssemblyPath { get; set; }
        [JsonProperty("enabled")]      public bool Enabled { get; set; } = true;
        [JsonProperty("description")]  public string Description { get; set; } = "";
    }
}
```

**`FrameworkConfig.cs`:**

```csharp
namespace my_revit_plugin.Configuration
{
    public class FrameworkConfig
    {
        [JsonProperty("commands")]
        public List<CommandConfig> Commands { get; set; } = new();

        [JsonProperty("settings")]
        public ServiceSettings Settings { get; set; } = new();
    }
}
```

**`ConfigurationManager.cs`:**

```csharp
namespace my_revit_plugin.Configuration
{
    public class ConfigurationManager
    {
        private readonly ILogger _logger;
        public FrameworkConfig Config { get; private set; }

        public ConfigurationManager(ILogger logger) { _logger = logger; }

        public void LoadConfiguration()
        {
            string path = PathManager.GetCommandRegistryFilePath();
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                Config = JsonConvert.DeserializeObject<FrameworkConfig>(json);
            }
        }
    }
}
```

#### Bước C5.3: Core - Registry & Manager (đăng ký commands)

**`RevitCommandRegistry.cs`** - Lưu mapping tên → command:

```csharp
namespace my_revit_plugin.Core
{
    public class RevitCommandRegistry : ICommandRegistry
    {
        private readonly Dictionary<string, IRevitCommand> _commands = new();

        public void RegisterCommand(IRevitCommand command)
        {
            _commands[command.CommandName] = command;
        }

        public bool TryGetCommand(string name, out IRevitCommand command)
        {
            return _commands.TryGetValue(name, out command);
        }

        public void ClearCommands() => _commands.Clear();
        public IEnumerable<string> GetRegisteredCommands() => _commands.Keys;
    }
}
```

**`CommandManager.cs`** - Load DLL bằng Reflection:

```csharp
namespace my_revit_plugin.Core
{
    public class CommandManager
    {
        private readonly ICommandRegistry _registry;
        private readonly ILogger _logger;
        private readonly ConfigurationManager _config;
        private readonly UIApplication _uiApp;

        public CommandManager(ICommandRegistry registry, ILogger logger,
            ConfigurationManager config, UIApplication uiApp)
        {
            _registry = registry;
            _logger = logger;
            _config = config;
            _uiApp = uiApp;
        }

        public void LoadCommands()
        {
            foreach (var cmd in _config.Config.Commands)
            {
                if (!cmd.Enabled) continue;

                // Tìm đường dẫn assembly
                string assemblyPath = cmd.AssemblyPath;
                if (!Path.IsPathRooted(assemblyPath))
                    assemblyPath = Path.Combine(PathManager.GetCommandsDirectoryPath(), assemblyPath);

                if (!File.Exists(assemblyPath)) continue;

                // Load assembly bằng Reflection
                Assembly assembly = Assembly.LoadFrom(assemblyPath);

                foreach (Type type in assembly.GetTypes())
                {
                    // Tìm class implement IRevitCommand
                    if (!typeof(IRevitCommand).IsAssignableFrom(type)) continue;
                    if (type.IsInterface || type.IsAbstract) continue;

                    // Tạo instance với UIApplication constructor
                    var ctor = type.GetConstructor(new[] { typeof(UIApplication) });
                    IRevitCommand command;
                    if (ctor != null)
                        command = (IRevitCommand)ctor.Invoke(new object[] { _uiApp });
                    else
                        command = (IRevitCommand)Activator.CreateInstance(type);

                    // Đăng ký nếu tên khớp
                    if (command.CommandName == cmd.CommandName)
                    {
                        _registry.RegisterCommand(command);
                        _logger.Info($"Loaded command: {command.CommandName}");
                        break;
                    }
                }
            }
        }
    }
}
```

**`ExternalEventManager.cs`** - Cache ExternalEvent:

```csharp
namespace my_revit_plugin.Core
{
    public class ExternalEventManager
    {
        private static ExternalEventManager _instance;
        private readonly Dictionary<string, (ExternalEvent Event, IWaitableExternalEventHandler Handler)> _events = new();
        private UIApplication _uiApp;
        private ILogger _logger;

        public static ExternalEventManager Instance => _instance ??= new();

        public void Initialize(UIApplication uiApp, ILogger logger)
        {
            _uiApp = uiApp;
            _logger = logger;
        }

        public ExternalEvent GetOrCreateEvent(IWaitableExternalEventHandler handler, string key)
        {
            if (_events.TryGetValue(key, out var wrapper) && wrapper.Handler == handler)
                return wrapper.Event;

            var externalEvent = ExternalEvent.Create(handler);
            _events[key] = (externalEvent, handler);
            return externalEvent;
        }
    }
}
```

#### Bước C5.4: Core - SocketService (TCP server)

```csharp
namespace my_revit_plugin.Core
{
    public class SocketService
    {
        private static SocketService _instance;
        public static SocketService Instance => _instance ??= new();

        private TcpListener _listener;
        private Thread _listenerThread;
        private bool _isRunning;
        private int _port = 8080;
        private ICommandRegistry _registry;
        private ILogger _logger;

        public bool IsRunning => _isRunning;

        public void Initialize(UIApplication uiApp)
        {
            _logger = new Logger();
            _registry = new RevitCommandRegistry();

            // Khởi tạo ExternalEvent manager
            ExternalEventManager.Instance.Initialize(uiApp, _logger);

            // Load cấu hình
            var configManager = new ConfigurationManager(_logger);
            configManager.LoadConfiguration();

            // Load commands từ DLL
            var cmdManager = new CommandManager(_registry, _logger, configManager, uiApp);
            cmdManager.LoadCommands();
        }

        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();

            _listenerThread = new Thread(ListenForClients) { IsBackground = true };
            _listenerThread.Start();
        }

        public void Stop()
        {
            _isRunning = false;
            _listener?.Stop();
        }

        // Lắng nghe kết nối TCP
        private void ListenForClients()
        {
            while (_isRunning)
            {
                TcpClient client = _listener.AcceptTcpClient();
                new Thread(HandleClient) { IsBackground = true }.Start(client);
            }
        }

        // Xử lý mỗi client connection
        private void HandleClient(object clientObj)
        {
            var tcpClient = (TcpClient)clientObj;
            var stream = tcpClient.GetStream();
            var buffer = new byte[8192];

            try
            {
                while (_isRunning && tcpClient.Connected)
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    string response = ProcessRequest(message);

                    byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                    stream.Write(responseBytes, 0, responseBytes.Length);
                }
            }
            finally { tcpClient.Close(); }
        }

        // Parse JSON-RPC → tìm command → execute → trả kết quả
        private string ProcessRequest(string json)
        {
            try
            {
                var request = JsonConvert.DeserializeObject<JsonRPCRequest>(json);

                if (!_registry.TryGetCommand(request.Method, out var command))
                    return ErrorResponse(request.Id, -32601, $"Method '{request.Method}' not found");

                object result = command.Execute(request.GetParamsObject(), request.Id);
                return SuccessResponse(request.Id, result);
            }
            catch (Exception ex)
            {
                return ErrorResponse(null, -32603, ex.Message);
            }
        }

        private string SuccessResponse(string id, object result)
        {
            return new JsonRPCSuccessResponse
            {
                Id = id,
                Result = result is JToken jt ? jt : JToken.FromObject(result)
            }.ToJson();
        }

        private string ErrorResponse(string id, int code, string message)
        {
            return new JsonRPCErrorResponse
            {
                Id = id,
                Error = new JsonRPCError { Code = code, Message = message }
            }.ToJson();
        }
    }
}
```

#### Bước C5.5: Core - Application (entry point)

```csharp
namespace my_revit_plugin.Core
{
    public class Application : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            // Tạo ribbon tab + panel
            RibbonPanel panel = application.CreateRibbonPanel("My MCP Plugin");

            // Nút bật/tắt MCP server
            var toggleBtn = new PushButtonData(
                "ToggleMCP", "MCP\r\nSwitch",
                Assembly.GetExecutingAssembly().Location,
                "my_revit_plugin.Core.MCPServiceConnection");
            toggleBtn.ToolTip = "Start/Stop MCP server";
            panel.AddItem(toggleBtn);

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            if (SocketService.Instance.IsRunning)
                SocketService.Instance.Stop();
            return Result.Succeeded;
        }
    }
}
```

#### Bước C5.6: Core - MCPServiceConnection (toggle button)

```csharp
namespace my_revit_plugin.Core
{
    [Transaction(TransactionMode.Manual)]
    public class MCPServiceConnection : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var service = SocketService.Instance;

            if (service.IsRunning)
            {
                service.Stop();
                TaskDialog.Show("MCP", "Server stopped");
            }
            else
            {
                service.Initialize(commandData.Application);
                service.Start();
                TaskDialog.Show("MCP", "Server started on port 8080");
            }
            return Result.Succeeded;
        }
    }
}
```

### C6. Workflow trong Plugin - Sơ đồ

```
Revit khởi động
    │
    ▼
Application.OnStartup()
    │ Tạo ribbon panel + nút "MCP Switch"
    ▼
User nhấn nút "MCP Switch"
    │
    ▼
MCPServiceConnection.Execute()
    │
    ├── Lần 1: service.Initialize() + service.Start()
    │   │
    │   ├── Initialize():
    │   │   ├── ExternalEventManager.Initialize(uiApp)
    │   │   ├── ConfigurationManager.LoadConfiguration()
    │   │   │   └── Đọc commandRegistry.json
    │   │   └── CommandManager.LoadCommands()
    │   │       ├── Duyệt config.Commands
    │   │       ├── Assembly.LoadFrom(dll_path)
    │   │       ├── Tìm class implement IRevitCommand
    │   │       └── registry.RegisterCommand(command)
    │   │
    │   └── Start():
    │       ├── TcpListener.Start() trên port 8080
    │       └── Thread → ListenForClients()
    │
    └── Lần 2: service.Stop()
        └── TcpListener.Stop()
```

---

## Phần D: Tạo Project 2 - CommandSet

### D1. Add Project vào Solution

```
Chuột phải Solution → Add → New Project
→ Class Library (C#)
→ Project name: MyCommandSet
→ Framework: .NET 8.0
```

### D2. Chỉnh .csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <UseWPF>true</UseWPF>
        <PlatformTarget>x64</PlatformTarget>
        <TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
        <ImplicitUsings>true</ImplicitUsings>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
        <PackageReference Include="Nice3point.Revit.Api.RevitAPI" Version="2025.*" />
        <PackageReference Include="Nice3point.Revit.Api.RevitAPIUI" Version="2025.*" />
        <PackageReference Include="RevitMCPSDK" Version="2025.*" />
    </ItemGroup>
</Project>
```

### D3. Cấu trúc thư mục

```
commandset/
├── MyCommandSet.csproj
├── Commands/            ← Mỗi command 1 file
│   └── GetViewInfoCommand.cs
├── Services/            ← Mỗi handler 1 file
│   └── GetViewInfoEventHandler.cs
└── Models/              ← DTOs
    └── ViewInfoResult.cs
```

### D4. Viết 1 command hoàn chỉnh (ví dụ: get_view_info)

#### D4.1: Model

```csharp
// Models/ViewInfoResult.cs
namespace MyCommandSet.Models
{
    public class ViewInfoResult
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string ViewType { get; set; }
        public int Scale { get; set; }
    }
}
```

#### D4.2: EventHandler (chạy trên Revit thread)

```csharp
// Services/GetViewInfoEventHandler.cs
namespace MyCommandSet.Services
{
    public class GetViewInfoEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        // ① Cơ chế đồng bộ (chờ Revit thread xong)
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        // ② Kết quả trả về
        public ViewInfoResult Result { get; private set; }

        // ③ Revit gọi method này trên main thread
        public void Execute(UIApplication uiapp)
        {
            try
            {
                var view = uiapp.ActiveUIDocument.Document.ActiveView;
                Result = new ViewInfoResult
                {
                    Id = (int)view.Id.Value,
                    Name = view.Name,
                    ViewType = view.ViewType.ToString(),
                    Scale = view.Scale
                };
            }
            catch (Exception ex)
            {
                Result = null;
            }
            finally
            {
                _resetEvent.Set();  // ④ Báo "xong rồi!"
            }
        }

        // ⑤ Command gọi method này để chờ
        public bool WaitForCompletion(int timeoutMs = 10000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMs);
        }

        public string GetName() => "GetViewInfo";
    }
}
```

#### D4.3: Command (nhận request, điều phối)

```csharp
// Commands/GetViewInfoCommand.cs
namespace MyCommandSet.Commands
{
    public class GetViewInfoCommand : ExternalEventCommandBase
    {
        private GetViewInfoEventHandler _handler =>
            (GetViewInfoEventHandler)Handler;

        // ① Tên command - phải khớp với MCP tool name
        public override string CommandName => "get_view_info";

        // ② Constructor nhận UIApplication
        public GetViewInfoCommand(UIApplication uiApp)
            : base(new GetViewInfoEventHandler(), uiApp) { }

        // ③ Được gọi khi nhận JSON-RPC request
        public override object Execute(JObject parameters, string requestId)
        {
            // Raise ExternalEvent → chờ handler chạy trên Revit thread
            if (RaiseAndWaitForCompletion(10000))
                return _handler.Result;
            throw new TimeoutException("Timeout getting view info");
        }
    }
}
```

### D5. Workflow trong CommandSet - Sơ đồ

```
SocketService nhận JSON-RPC: { method: "get_view_info" }
    │
    ▼
registry.TryGetCommand("get_view_info") → GetViewInfoCommand
    │
    ▼
GetViewInfoCommand.Execute(params, requestId)
    │
    ├── ① _handler.SetParameters(data)     ← Nếu có params
    │
    ├── ② RaiseAndWaitForCompletion(10000)
    │   │
    │   ├── ExternalEvent.Raise()           ← Đưa vào queue Revit
    │   │
    │   └── handler.WaitForCompletion()     ← ManualResetEvent.WaitOne()
    │       │                                  (block thread TCP)
    │       │
    │       │   ┌──── Revit Main Thread ────┐
    │       │   │                            │
    │       │   │ handler.Execute(uiApp)     │
    │       │   │   │                        │
    │       │   │   ├── doc.ActiveView       │
    │       │   │   ├── Gọi Revit API        │
    │       │   │   ├── Result = ...         │
    │       │   │   └── _resetEvent.Set()    │← Unblock!
    │       │   │                            │
    │       │   └────────────────────────────┘
    │       │
    │       └── return true (hoặc false nếu timeout)
    │
    ├── ③ return _handler.Result
    │
    ▼
SocketService gửi JSON-RPC response về MCP Server
```

### D6. Tạo `command.json`

Tạo ở gốc dự án:

```json
{
  "name": "MyCommandSet",
  "description": "My custom commands",
  "commands": [
    {
      "commandName": "get_view_info",
      "description": "Get current view information",
      "assemblyPath": "MyCommandSet.dll"
    }
  ]
}
```

### D7. Tạo `commandRegistry.json`

File này nằm trong `Commands/` folder khi deploy:

```json
{
  "commands": [
    {
      "commandName": "get_view_info",
      "assemblyPath": "MyCommandSet/2025/MyCommandSet.dll",
      "enabled": true,
      "description": "Get current view information"
    }
  ],
  "settings": {
    "logLevel": "Info",
    "port": 8080
  }
}
```

---

## Phần E: Tạo MCP Server (TypeScript)

### E1. Khởi tạo project

```bash
mkdir server && cd server
npm init -y
npm install @modelcontextprotocol/sdk zod ws
npm install -D typescript @types/node @types/ws rimraf
```

### E2. `tsconfig.json`

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "Node16",
    "moduleResolution": "Node16",
    "outDir": "./build",
    "rootDir": "./src",
    "strict": true,
    "esModuleInterop": true
  },
  "include": ["src/**/*"]
}
```

### E3. `package.json` (thêm vào)

```json
{
  "type": "module",
  "bin": {
    "my-mcp-server": "./build/index.js"
  },
  "scripts": {
    "build": "rimraf build && tsc",
    "start": "node build/index.js"
  }
}
```

### E4. Entry point `src/index.ts`

```typescript
#!/usr/bin/env node
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { registerTools } from "./tools/register.js";

const server = new McpServer({
  name: "my-mcp-server",
  version: "1.0.0",
});

async function main() {
  await registerTools(server);
  const transport = new StdioServerTransport();
  await server.connect(transport);
  console.error("MCP Server started");
}

main().catch(console.error);
```

### E5. Tool registration `src/tools/register.ts`

```typescript
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

export async function registerTools(server: McpServer) {
  const __dirname = path.dirname(fileURLToPath(import.meta.url));
  const files = fs.readdirSync(__dirname);

  for (const file of files) {
    if (file === "register.js" || file === "register.ts") continue;
    if (!file.endsWith(".js") && !file.endsWith(".ts")) continue;

    const module = await import(`./${file.replace(/\.ts$/, ".js")}`);
    const registerFn = Object.keys(module).find(k => k.startsWith("register"));
    if (registerFn) module[registerFn](server);
  }
}
```

### E6. Socket Client `src/utils/SocketClient.ts`

```typescript
import * as net from "net";

export class RevitClient {
  private socket: net.Socket;
  private callbacks: Map<string, (data: string) => void> = new Map();
  private buffer = "";
  isConnected = false;

  constructor(private host: string, private port: number) {
    this.socket = new net.Socket();

    this.socket.on("connect", () => { this.isConnected = true; });
    this.socket.on("close", () => { this.isConnected = false; });
    this.socket.on("data", (data) => {
      this.buffer += data.toString();
      try {
        JSON.parse(this.buffer);
        this.handleResponse(this.buffer);
        this.buffer = "";
      } catch { /* incomplete, wait for more */ }
    });
  }

  connect() { this.socket.connect(this.port, this.host); }
  disconnect() { this.socket.end(); this.isConnected = false; }

  private handleResponse(data: string) {
    const response = JSON.parse(data);
    const cb = this.callbacks.get(response.id);
    if (cb) { cb(data); this.callbacks.delete(response.id); }
  }

  sendCommand(method: string, params: any = {}): Promise<any> {
    return new Promise((resolve, reject) => {
      const id = Date.now().toString() + Math.random().toString().slice(2, 8);
      const request = { jsonrpc: "2.0", method, params, id };

      this.callbacks.set(id, (data) => {
        const res = JSON.parse(data);
        if (res.error) reject(new Error(res.error.message));
        else resolve(res.result);
      });

      this.socket.write(JSON.stringify(request));

      setTimeout(() => {
        if (this.callbacks.has(id)) {
          this.callbacks.delete(id);
          reject(new Error(`Timeout: ${method}`));
        }
      }, 120000);
    });
  }
}
```

### E7. Connection Manager `src/utils/ConnectionManager.ts`

```typescript
import { RevitClient } from "./SocketClient.js";

let mutex: Promise<void> = Promise.resolve();

export async function withRevitConnection<T>(
  operation: (client: RevitClient) => Promise<T>
): Promise<T> {
  // Mutex: chỉ 1 kết nối tại 1 thời điểm
  const prev = mutex;
  let release: () => void;
  mutex = new Promise(r => { release = r; });
  await prev;

  const client = new RevitClient("localhost", 8080);
  try {
    await new Promise<void>((resolve, reject) => {
      client.socket.on("connect", resolve);
      client.socket.on("error", reject);
      client.connect();
      setTimeout(() => reject(new Error("Connection timeout")), 5000);
    });
    return await operation(client);
  } finally {
    client.disconnect();
    release!();
  }
}
```

### E8. Tool definition `src/tools/get_view_info.ts`

```typescript
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetViewInfoTool(server: McpServer) {
  server.tool(
    "get_view_info",                              // Tên tool
    "Get current active view information",        // Mô tả cho AI
    {},                                           // Schema (không có params)
    async () => {
      try {
        const result = await withRevitConnection(client =>
          client.sendCommand("get_view_info", {})
        );
        return {
          content: [{ type: "text", text: JSON.stringify(result, null, 2) }]
        };
      } catch (error) {
        return {
          content: [{ type: "text", text: `Error: ${error.message}` }]
        };
      }
    }
  );
}
```

### E9. Workflow MCP Server - Sơ đồ

```
AI gọi tool "get_view_info"
    │
    ▼
McpServer nhận qua StdioTransport (stdin)
    │
    ▼
Tool handler được gọi
    │
    ▼
withRevitConnection(async (client) => ...)
    │
    ├── Acquire mutex (chờ request trước xong)
    │
    ├── new RevitClient("localhost", 8080)
    │
    ├── client.connect()  ← TCP connect
    │
    ├── client.sendCommand("get_view_info", {})
    │   │
    │   ├── Tạo JSON-RPC: { jsonrpc:"2.0", method:"get_view_info", id:"..." }
    │   ├── socket.write(JSON)
    │   └── Chờ response callback (timeout 2 phút)
    │
    ├── Nhận response → resolve result
    │
    ├── client.disconnect()
    │
    └── Release mutex
    │
    ▼
Return { content: [{ type: "text", text: JSON.stringify(result) }] }
    │
    ▼
McpServer gửi qua StdioTransport (stdout) → AI nhận kết quả
```

---

## Phần F: Kết nối tất cả lại - Workflow hoàn chỉnh

### Sơ đồ End-to-End

```
                    AI (Claude, Cursor...)
                           │
          ① Gọi tool "get_view_info"
                           │ stdin (MCP protocol)
                           ▼
               ┌─── MCP Server (TS) ───┐
               │                        │
               │ ② server.tool() handler│
               │ ③ withRevitConnection()│
               │ ④ client.sendCommand() │
               └───────────┬────────────┘
                           │ TCP localhost:8080
                    ⑤ JSON-RPC Request:
                    {
                      "jsonrpc": "2.0",
                      "method": "get_view_info",
                      "params": {},
                      "id": "abc123"
                    }
                           │
                           ▼
               ┌─── Revit Plugin (C#) ──┐
               │                         │
               │ ⑥ SocketService nhận msg│
               │ ⑦ ProcessRequest()      │
               │ ⑧ registry.TryGetCommand│
               │    → GetViewInfoCommand │
               └───────────┬─────────────┘
                           │
                           ▼
               ┌─── CommandSet (C#) ────┐
               │                         │
               │ ⑨ Command.Execute()     │
               │ ⑩ ExternalEvent.Raise() │
               │    ↓ (Revit thread)     │
               │ ⑪ Handler.Execute(uiApp)│
               │    ├── doc.ActiveView   │
               │    ├── Result = ...     │
               │    └── _resetEvent.Set()│
               │ ⑫ return handler.Result │
               └───────────┬─────────────┘
                           │
                    ⑬ JSON-RPC Response:
                    {
                      "jsonrpc": "2.0",
                      "id": "abc123",
                      "result": {
                        "Id": 12345,
                        "Name": "Level 1",
                        "ViewType": "FloorPlan"
                      }
                    }
                           │ TCP
                           ▼
               MCP Server nhận response
                           │ stdout (MCP protocol)
                           ▼
                    ⑭ AI nhận kết quả
```

### Tóm tắt 14 bước:

| # | Ở đâu | Hành động |
|---|--------|-----------|
| 1 | AI | Gọi MCP tool `get_view_info` |
| 2 | MCP Server | `server.tool()` handler kích hoạt |
| 3 | MCP Server | `withRevitConnection()` acquire mutex, tạo TCP client |
| 4 | MCP Server | `sendCommand()` tạo JSON-RPC request |
| 5 | Network | JSON-RPC truyền qua TCP:8080 |
| 6 | Plugin | `SocketService` nhận bytes, decode UTF-8 |
| 7 | Plugin | `ProcessRequest()` parse JSON-RPC |
| 8 | Plugin | `registry.TryGetCommand("get_view_info")` tìm command |
| 9 | CommandSet | `GetViewInfoCommand.Execute()` được gọi |
| 10 | CommandSet | `ExternalEvent.Raise()` đưa vào queue Revit |
| 11 | Revit Thread | `Handler.Execute(uiApp)` gọi Revit API |
| 12 | CommandSet | Command nhận `handler.Result`, return |
| 13 | Plugin→Network | JSON-RPC response gửi qua TCP |
| 14 | MCP Server→AI | Kết quả trả về AI qua stdout |

---

## Phần G: Build, Deploy và Debug

### G1. Build

Trong Visual Studio:

```
1. Chọn Configuration: "Debug" (hoặc "Debug R25" nếu multi-version)
2. Build → Build Solution (Ctrl+Shift+B)
```

Output:
- Plugin DLL → `plugin/bin/Debug/2025/`
- CommandSet DLL → `commandset/bin/Debug/2025/`

### G2. Deploy thủ công (nếu không có post-build)

Copy vào thư mục Revit Addins:

```
%AppData%\Autodesk\Revit\Addins\2025\
├── my-plugin.addin                      ← File .addin
└── my_revit_plugin/                     ← Plugin DLLs
    ├── MyRevitPlugin.dll
    ├── Newtonsoft.Json.dll
    └── Commands/                        ← CommandSet
        └── MyCommandSet/
            ├── command.json
            ├── commandRegistry.json
            └── 2025/
                └── MyCommandSet.dll
```

### G3. Debug với Visual Studio

```
1. Set Startup Project = MyRevitPlugin
2. Project Properties → Debug:
   - Start external program: C:\Program Files\Autodesk\Revit 2025\Revit.exe
   - Command line arguments: /language ENG
3. F5 → Revit mở → Plugin load → Đặt breakpoint → Debug bình thường
```

### G4. Build MCP Server

```bash
cd server
npm install
npm run build
```

### G5. Test kết nối

```
1. Mở Revit → Nhấn nút "MCP Switch" → "Server started"
2. Cấu hình Claude Desktop:
   {
     "mcpServers": {
       "my-mcp-server": {
         "command": "node",
         "args": ["path/to/server/build/index.js"]
       }
     }
   }
3. Trong Claude: "Get the current view info"
4. Claude gọi tool → kết quả trả về
```

---

## Phần H: Sơ đồ tổng hợp các bước

```
╔══════════════════════════════════════════════════════════╗
║              BƯỚC TẠO DỰ ÁN TỪ ĐẦU                    ║
╠══════════════════════════════════════════════════════════╣
║                                                          ║
║  B1. Tạo Blank Solution trong VS                        ║
║       │                                                  ║
║  B2. Add Project "Plugin" (Class Library)               ║
║       │                                                  ║
║       ├── Chỉnh .csproj (net8.0, x64, WPF, NuGet)      ║
║       ├── Tạo .addin file                                ║
║       ├── Viết Utils (PathManager, Logger)              ║
║       ├── Viết Configuration models                     ║
║       ├── Viết Core:                                    ║
║       │   ├── RevitCommandRegistry                      ║
║       │   ├── CommandManager                            ║
║       │   ├── ExternalEventManager                      ║
║       │   ├── SocketService (TCP:8080)                  ║
║       │   ├── MCPServiceConnection (toggle button)      ║
║       │   └── Application (entry point)                 ║
║       │                                                  ║
║  B3. Add Project "CommandSet" (Class Library)           ║
║       │                                                  ║
║       ├── Chỉnh .csproj                                 ║
║       ├── Tạo Models (DTOs)                             ║
║       ├── Viết EventHandler:                            ║
║       │   ├── Implement IExternalEventHandler           ║
║       │   ├── Implement IWaitableExternalEventHandler   ║
║       │   ├── ManualResetEvent cho đồng bộ              ║
║       │   └── Execute() = gọi Revit API                ║
║       ├── Viết Command:                                 ║
║       │   ├── Extend ExternalEventCommandBase           ║
║       │   ├── CommandName = "tên_tool"                  ║
║       │   └── Execute() = parse params + raise event    ║
║       │                                                  ║
║  B4. Tạo command.json + commandRegistry.json            ║
║       │                                                  ║
║  B5. Tạo MCP Server (TypeScript)                        ║
║       │                                                  ║
║       ├── npm init + install dependencies               ║
║       ├── Viết SocketClient (TCP client)                ║
║       ├── Viết ConnectionManager (mutex)                ║
║       ├── Viết tool files (zod schema + handler)        ║
║       ├── Viết register.ts (auto-load)                  ║
║       └── Viết index.ts (entry point)                   ║
║       │                                                  ║
║  B6. Build + Deploy + Test                              ║
║       │                                                  ║
║       ├── Build C# → Copy DLLs vào Revit Addins        ║
║       ├── Build TS → npm run build                       ║
║       ├── Mở Revit → Bật MCP Switch                    ║
║       └── Cấu hình AI client → Test tool calls          ║
║                                                          ║
╚══════════════════════════════════════════════════════════╝
```

---

## Ghi nhớ quan trọng

### Tại sao cần ExternalEvent?

Revit API **chỉ cho phép gọi từ main UI thread**. TCP request đến từ background thread. `ExternalEvent` là cơ chế duy nhất của Revit để "nhờ" main thread thực thi code.

### Tại sao cần ManualResetEvent?

Khi TCP thread gọi `ExternalEvent.Raise()`, nó cần **chờ** cho đến khi Revit thread chạy xong handler. `ManualResetEvent` đóng vai trò cờ hiệu: handler gọi `Set()` khi xong, TCP thread gọi `WaitOne()` để chờ.

### Tại sao cần Mutex ở MCP Server?

Revit chỉ xử lý **1 request tại 1 thời điểm**. Nếu 2 tool gọi song song, sẽ bị conflict. Mutex đảm bảo serialize tất cả requests.

### Đơn vị

| AI gửi | Revit API dùng | Hệ số chuyển đổi |
|---------|---------------|-------------------|
| mm | feet | ÷ 304.8 |

```csharp
// Ví dụ: AI gửi x=5000 (mm) → Revit cần 5000/304.8 = 16.4 (ft)
new XYZ(point.X / 304.8, point.Y / 304.8, point.Z / 304.8);
```
