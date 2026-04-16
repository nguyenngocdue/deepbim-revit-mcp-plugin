# revit-mcp-plugin — Project Context for AI Agents

## What This Project Does

Bridges **AI clients (Claude, Cursor, Cline)** ↔ **Autodesk Revit** via the MCP protocol.
AI sends commands → MCP Server (TypeScript) → TCP JSON-RPC → Revit Plugin (C#) → CommandSet (C#) → Revit API.

## Skill Files (`.agents/`)

| File | Content |
|------|---------|
| [.agents/architecture.md](.agents/architecture.md) | Full project structure: plugin, commandset, tools projects |
| [.agents/server.md](.agents/server.md) | MCP Server (TypeScript): transport modes, endpoints, tools list |
| [.agents/patterns.md](.agents/patterns.md) | How to add a command, ExternalEvent pattern, JSON-RPC flow, coordinates |
| [.agents/commands.md](.agents/commands.md) | All available MCP commands with descriptions |

---

## Architecture (3 Layers)

```
AI Client (Claude/Cursor/Cline)
    │ stdio (MCP Protocol)
    ▼
revit-mcp-server/  ← TypeScript MCP Server  (Node.js) — see .agents/server.md
    │ TCP localhost:8080 (JSON-RPC 2.0)
    ▼
plugin/            ← Revit Add-in (C#, runs inside Revit process)
    │ ExternalEvent (switch to Revit main thread)
    ▼
commandset/        ← Command implementations (C#, loaded as DLL at runtime)
    │ Revit API
    ▼
Autodesk Revit
```
