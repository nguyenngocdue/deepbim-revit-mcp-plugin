# MCP Server — revit-mcp-server

**Location:** `E:\C# Tool Revit\revit-mcp\revit-mcp-server\`
**Stack:** TypeScript, Node.js ≥20, pnpm, MCP SDK

## Transport Modes

| Mode | Trigger | Use case |
|------|---------|----------|
| **stdio** | `MCP_TRANSPORT` not set AND `PORT` not set | Local AI clients (Claude Desktop, Cursor, Cline) |
| **HTTP** | `MCP_TRANSPORT=http` OR `PORT` is set | Cloud deploy (Render.com), remote access |

### HTTP Endpoints

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/` | Landing page — uptime, tool count |
| `GET` | `/health` | Health check (used by Render.com) |
| `GET` | `/mcp/tools` | List all registered tools (JSON, no auth) |
| `POST` | `/mcp` | MCP Streamable HTTP — stateless, `StreamableHTTPServerTransport` |
| `GET` | `/mcp` | 405 hint |

Port: `process.env.PORT || 3000`

## Project Structure

```
revit-mcp-server/
├── package.json           ← pnpm, name: revit-mcp-server, main: build/index.js
├── tsconfig.json
├── Dockerfile             ← Docker support
├── render.yaml            ← Render.com deploy config
├── .env                   ← PORT, API_KEY, MCP_TRANSPORT
├── src/
│   ├── index.ts           ← Entry point: dual transport, Express app, API_KEY gen
│   ├── tools/             ← One .ts file per tool (auto-registered)
│   │   ├── register.ts    ← Scans dir → imports each file → calls register*() fn
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
│   │   ├── store_project_data.ts  ← Saves data to SQLite
│   │   ├── store_room_data.ts
│   │   ├── query_stored_data.ts   ← Queries SQLite
│   │   ├── search_modules.ts
│   │   ├── use_module.ts
│   │   ├── send_code_to_revit.ts  ← Sends C# code to Revit for runtime execution
│   │   ├── hello_world.ts
│   │   └── say_hello.ts
│   ├── utils/
│   │   ├── ConnectionManager.ts   ← Mutex + TCP connection to Revit (localhost:8080)
│   │   └── SocketClient.ts        ← JSON-RPC 2.0 client over TCP socket
│   └── database/
│       └── service.ts             ← better-sqlite3: store/query project & room data
├── build/                         ← Compiled JS (pnpm build → tsc)
└── doc/
    ├── guide-to-build-server.md
    ├── guide-to-deploy-render.md
    └── huong-dan-trien-khai.md
```

## Adding a New Tool

1. Create `src/tools/my_tool_name.ts`
2. Export a function named `registerMyToolName(server: McpServer)`
3. Inside, call `server.tool(name, description, zodSchema, handler)`
4. Handler calls `withRevitConnection(client => client.sendCommand("command_name", args))`
5. `register.ts` picks it up automatically — no import needed

## Connecting to Revit Plugin (Port Forwarding)

When the MCP server runs in the **cloud** (Render.com), it cannot reach `localhost:8080` of the user's machine.
Use a **TCP tunnel** to expose the Revit plugin port publicly.

### Setup with ngrok

```bash
# 1. Expose Revit plugin port via ngrok TCP tunnel
ngrok tcp 8080
# → Forwarding: tcp://0.tcp.ngrok.io:12345

# 2. Set env vars on Render.com (or .env for local testing)
REVIT_HOST=0.tcp.ngrok.io
REVIT_PORT=12345
```

### How it works

```
AI Client (Claude/Cursor)
    │ HTTP POST /mcp
    ▼
Render.com (MCP Server)
    │ TCP  0.tcp.ngrok.io:12345   ← REVIT_HOST / REVIT_PORT
    ▼
ngrok tunnel
    │ TCP localhost:8080
    ▼
Revit Plugin (user's machine)
```

- If `REVIT_HOST` / `REVIT_PORT` are **not set** → auto-scan `localhost:8080-8099` (default, local mode)
- If **set** → connect directly to that host:port (skip scan)

### Alternatives to ngrok

| Tool | Command | Notes |
|------|---------|-------|
| **ngrok** | `ngrok tcp 8080` | Free tier available |
| **cloudflared** | `cloudflare tunnel --url tcp://localhost:8080` | Cloudflare free |
| **VS Code Port Forwarding** | Forward port 8080 in VS Code | Works with GitHub account |

---

## Build & Run

```bash
pnpm install
pnpm build          # tsc → build/

# stdio (local, no env vars needed)
node build/index.js

# HTTP mode (port 3000)
PORT=3000 node build/index.js

# HTTP mode + connect to remote Revit via ngrok
PORT=3000 REVIT_HOST=0.tcp.ngrok.io REVIT_PORT=12345 node build/index.js

# Inspect with MCP Inspector
pnpm inspect
```

Copy `.env.example` → `.env` and fill in values before running.
