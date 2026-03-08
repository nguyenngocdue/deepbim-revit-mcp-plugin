# DeepBim-MCP

**AI-Powered BIM Automation for Autodesk Revit via the Model Context Protocol.**

DeepBim-MCP enables AI assistants like Claude, Cursor, and other MCP-compatible tools to interact with Revit projects. It consists of three components: a TypeScript MCP server that exposes tools to AI clients, a C# Revit add-in that bridges commands into Revit, and a command set that implements the Revit API operations.

> **Acknowledgement:** This project references and draws inspiration from the architecture of [mcp-servers-for-revit](https://github.com/mcp-servers-for-revit/mcp-servers-for-revit). We recommend reviewing their codebase for additional patterns and tool implementations.

## Architecture

```
┌─────────────────┐     stdio      ┌──────────────────┐     TCP      ┌─────────────────┐
│  AI Client      │ ◄────────────► │  MCP Server      │ ◄──────────► │  Revit Plugin   │
│  (Claude, etc.) │                │  (Node.js)       │  8080-8099   │  (C# Add-in)     │
└─────────────────┘                └──────────────────┘              └────────┬────────┘
                                                                              │
                                                                              ▼
                                                                     ┌─────────────────┐
                                                                     │  Command Set    │
                                                                     │  (Revit API)    │
                                                                     └─────────────────┘
```

- **MCP Server** (TypeScript): Translates tool calls from AI clients into TCP messages
- **Revit Plugin** (C#): Runs inside Revit, listens on port 8080–8099, dispatches to Command Set
- **Command Set** (C#): Executes Revit API operations and returns results

## Requirements

- **Node.js 18+** (for the MCP server)
- **Autodesk Revit 2025** (or compatible version)
- **.NET 8** (for plugin and command set)

## Quick Start

### 1. Build the Project

```bash
# Build MCP Server
cd server
npm install
npm run build

# Build Plugin + Command Set (Visual Studio or dotnet CLI)
# Open revit-mcp-plugin.sln and build, or:
dotnet build revit-mcp-plugin.sln -c Debug
```

### 2. Install the Revit Add-in

Run the setup script (or copy manually):

```powershell
.\setup-revit-addin.bat
```

Or copy the contents of `plugin\bin\AddIn 2025 Debug\` to:

```
%AppData%\Autodesk\Revit\Addins\2025\
├── revit-mcp-plugin.addin
└── revit_mcp_plugin\
    ├── RevitMCPPlugin.dll
    └── Commands\
        ├── commandRegistry.json
        └── RevitMCPCommandSet\
            ├── command.json
            └── 2025\
                └── RevitMCPCommandSet.dll
```

### 3. Start Revit

1. Open Revit 2025 and load a project
2. Go to **Add-Ins** tab → **DeepBim-MCP** → click **Connect Server**
3. In the **DeepBim-MCP Server Control** window, click **Start** (or it auto-starts)
4. Verify status shows **Running** and the port (e.g. 8081)

![DeepBim-MCP Server Control](images/MCP%20Server%20Control.png)

*DeepBim-MCP Server Control — trạng thái server, port, đường dẫn plugin và log.*

5. (Tùy chọn) Mở **Settings** để bật/tắt từng command set và từng lệnh MCP.

![DeepBim-MCP Settings](images/MCP-Settings.png)

*DeepBim-MCP Settings — Command Set Settings: chọn command set, bật/tắt từng command.*

### 4. Configure Claude Desktop

Edit `%APPDATA%\Claude\claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "deepbim-mcp-server": {
      "command": "node",
      "args": ["<path-to-project>/server/build/index.js"]
    }
  }
}
```

Use an absolute path to `server/build/index.js`. Restart Claude Desktop. When you see the 🔨 icon, the MCP server is connected.

## Supported Tools

| Tool                  | Description                                    |
| --------------------- | ---------------------------------------------- |
| `say_hello`           | Display a greeting dialog (connection test)    |
| `get_view_info`       | Get current active view information            |
| `get_selected_elements` | Get currently selected elements in Revit    |

## Project Structure

```
revit-mcp-plugin/
├── revit-mcp-plugin.sln
├── command.json              # Command set manifest
├── server/                   # MCP server (TypeScript)
│   ├── src/
│   │   ├── index.ts
│   │   ├── tools/            # Tool definitions
│   │   └── utils/            # Connection manager, socket client
│   └── build/
├── plugin/                   # Revit add-in (C#)
│   ├── Core/                 # SocketService, CommandManager, etc.
│   ├── UI/                   # MCP Status Window, Settings
│   └── Configuration/
├── commandset/               # Command implementations (C#)
│   ├── Commands/
│   └── Services/
├── setup-revit-addin.ps1     # Deploy script
├── test-connection.ps1       # Test TCP connection
└── CLAUDE-SETUP.md           # Detailed Claude setup guide
```

## Connection Flow

1. **Revit** must be running with the plugin **Started** (MCP Switch → Start)
2. **MCP Server** auto-discovers the plugin on ports 8080–8099
3. **Claude** calls a tool → MCP Server connects to Revit → Plugin executes → Result returned

## Troubleshooting

- **"Method not found"**: Ensure `commandRegistry.json` exists in `Commands/` and the Command Set DLL is in `Commands/RevitMCPCommandSet/2025/`
- **"No DeepBim-MCP server found"**: Revit plugin not started — open MCP Switch and click Start
- **"No matching tools found"**: Claude config incorrect or Claude not restarted — check `claude_desktop_config.json` and restart Claude

See [CLAUDE-SETUP.md](CLAUDE-SETUP.md) and [KET-NOI-CLAUDE-REVIT.md](KET-NOI-CLAUDE-REVIT.md) for detailed setup and connection guides.

## Reference

This project is inspired by and references the following open-source project:

- **[mcp-servers-for-revit](https://github.com/mcp-servers-for-revit/mcp-servers-for-revit)** — Sparx fork with extensive tools for Revit automation via MCP. We recommend exploring their codebase for additional patterns, tool implementations, and best practices.

## License

MIT
