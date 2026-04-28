# DeepBim Revit MCP Plugin v3.0.0

Release date: 2026-04-25  
License: MIT  
Platform: Windows x64  
Supported Revit versions: Autodesk Revit 2024, 2025, and 2026

DeepBim Revit MCP Plugin connects Autodesk Revit with MCP-compatible AI tools, allowing AI assistants to inspect models, read elements, and run supported Revit automation commands.

This v3 release adds support for connecting Revit with ChatGPT through the hosted DeepBim MCP workflow, while keeping the local MCP server workflow for Claude Desktop, VS Code, Cursor, and other MCP-compatible clients.

---

## 1. Overview

DeepBim Revit MCP Plugin supports two connection modes:

| Mode | Recommended For | Description |
|------|-----------------|-------------|
| ChatGPT Online MCP | Users who want to connect ChatGPT with Revit | ChatGPT connects to a hosted DeepBim MCP endpoint; the local Revit machine is exposed through a Cloudflare Tunnel |
| Local MCP Server | Developers, private workflows, internal use | Run the bundled MCP server locally through Node.js and connect from desktop MCP clients |

Hosted MCP endpoint:

```text
https://revit-mcp-server.onrender.com/mcp
```

Connection page:

```text
https://revit-mcp-server.onrender.com/connect
```

---

## 2. Highlights

- Supports Autodesk Revit 2024, 2025, and 2026.
- Adds ChatGPT connection support through hosted MCP.
- Includes the MCP server runtime inside the MSI installer.
- No need to clone the MCP server repository after installation.
- Supports local MCP clients such as Claude Desktop, VS Code, Cursor, and Cline.
- Supports Revit automation tools such as element query, room/level/grid creation, tagging, color overrides, sheet export, and dynamic code execution.

---

## 3. System Requirements

- Windows 10 or Windows 11 x64.
- Autodesk Revit 2024, 2025, or 2026.
- Node.js LTS installed and available as `node` in PATH.
- `cloudflared` installed if using ChatGPT Online MCP mode.
- No administrator permission is required for the normal per-user MSI install.
- ChatGPT account with Apps support if using ChatGPT Online MCP mode.

Runtime notes:

- Revit 2024 uses .NET Framework 4.8.
- Revit 2025 and 2026 use .NET 8.

---

## 4. Installation

1. Close Revit.
2. Download the MSI for your Revit version:
   - `DeepBimMCP-Revit2024-v3.0.0.msi`
   - `DeepBimMCP-Revit2025-v3.0.0.msi`
   - `DeepBimMCP-Revit2026-v3.0.0.msi`
3. Run the MSI installer.
4. Start Revit.
5. Open the Add-Ins tab.
6. Start the DeepBim MCP server connection from the DeepBim-MCP panel.

Installed add-in location:

```text
%APPDATA%\Autodesk\Revit\Addins\{RevitVersion}\
```

Bundled local MCP server entry:

```text
%APPDATA%\Autodesk\Revit\Addins\{RevitVersion}\DeepBimRevitMCPlugin\server\build\index.js
```

---

## 5. Quick Start: ChatGPT Online Mode

Use this option if you want to connect ChatGPT to Revit through the hosted DeepBim MCP endpoint.

This mode uses the Revit plugin's local HTTP endpoint and a Cloudflare Tunnel. ChatGPT connects to the hosted DeepBim MCP endpoint, and the hosted endpoint reaches the user's Revit machine through the registered tunnel URL.

Important: ChatGPT can access Revit only after the local machine is ready. Revit must be open, the DeepBim Revit MCP plugin must be loaded, the local HTTP server must be running on port `9080`, and the current Cloudflare Tunnel URL must be registered at the `/connect` page.

### Steps

1. Install `cloudflared` on the machine running Revit:

```powershell
winget install Cloudflare.cloudflared
```

2. Install the MSI package for your Revit version.

3. Restart Revit.

4. In Revit, open the DeepBim-MCP panel and start the server connection.

5. Verify the plugin is running and exposes HTTP port `9080`.

6. In a terminal on the Revit machine, start the Cloudflare Tunnel:

```powershell
cloudflared tunnel --url http://localhost:9080
```

7. Copy the generated public URL, for example:

```text
https://xxxx.trycloudflare.com
```

8. Open:

```text
https://revit-mcp-server.onrender.com/connect
```

9. Paste the Cloudflare Tunnel URL and click `Connect`.

10. Wait until the page confirms that the Revit URL was updated successfully.

11. Keep Revit open and keep the `cloudflared` terminal running.

12. Open ChatGPT.

13. Go to `Settings`.

14. Open `Apps`.

15. Select `Create app`.

16. Fill in the app information:

| Field | Value |
|-------|-------|
| Logo | Use the DeepBim logo |
| Name | `DeepBim Revit MCP` |
| Description | `Connect ChatGPT to Autodesk Revit through DeepBim MCP to inspect models and run supported automation commands.` |
| MCP Server URL | `https://revit-mcp-server.onrender.com/mcp` |
| Authentication | `No Auth` |

17. Save the app.

18. Enable the app in ChatGPT.

19. Ask ChatGPT to test the Revit connection, for example:

```text
Use DeepBim Revit MCP to say hello in Revit.
```

Every time the Cloudflare Tunnel is restarted, a new `trycloudflare.com` URL is generated. Paste the new URL into the `/connect` page, click `Connect`, and wait for the success message again.

Authentication is not implemented yet. Select `No Auth` when creating the ChatGPT app.

---

## 6. Local MCP Client Configuration

Use this option for Claude Desktop, VS Code, Cursor, Cline, or other local MCP clients.

After installation, configure your MCP client to run the bundled local server.

Path pattern:

```text
%APPDATA%\Autodesk\Revit\Addins\{RevitVersion}\DeepBimRevitMCPlugin\server\build\index.js
```

Example for Claude Desktop:

```json
{
  "mcpServers": {
    "deepbim-revit-mcp": {
      "command": "node",
      "args": [
        "C:\\Users\\YOUR_USER\\AppData\\Roaming\\Autodesk\\Revit\\Addins\\2026\\DeepBimRevitMCPlugin\\server\\build\\index.js"
      ]
    }
  }
}
```

Example for VS Code:

```json
{
  "servers": {
    "deepbim-revit-mcp": {
      "type": "stdio",
      "command": "node",
      "args": [
        "C:\\Users\\YOUR_USER\\AppData\\Roaming\\Autodesk\\Revit\\Addins\\2026\\DeepBimRevitMCPlugin\\server\\build\\index.js"
      ]
    }
  }
}
```

Update the path for your Windows user account and Revit version.

---

## 7. Usage Flow

### ChatGPT Online MCP Mode

```text
Install MSI
   ↓
Open Revit and start DeepBim MCP
   ↓
Run cloudflared tunnel to localhost:9080
   ↓
Register tunnel URL at /connect
   ↓
Create ChatGPT app with hosted MCP URL
   ↓
ChatGPT can call Revit tools
```

### Local MCP Server Mode

```text
Install MSI
   ↓
Open Revit and start DeepBim MCP
   ↓
Configure local MCP client to run bundled index.js
   ↓
MCP client starts the server
   ↓
AI client can call Revit tools
```

---

## 8. Security Notes

- The Revit plugin runs locally on the user's machine.
- ChatGPT Online MCP mode exposes local Revit access through a temporary Cloudflare Tunnel URL.
- Keep the Cloudflare Tunnel terminal open only while you need online access.
- Do not use confidential production models with online workflows unless this matches your organization's security policy.
- Review AI-generated operations before applying them to production models.
- Keep backups of important Revit files before automation-heavy workflows.
- Hosted ChatGPT mode currently uses `No Auth`.

---

## 9. Known Limitations

- Revit must be open for AI tools to access the active model.
- Node.js must be installed and available as `node` in PATH.
- ChatGPT Online MCP mode requires `cloudflared`.
- The Cloudflare Tunnel URL changes every time the tunnel is restarted.
- Some automation commands depend on the active Revit model state, loaded families, selected elements, and current view.
- Endpoint security tools may block unsigned or newly built plugin files until they are reviewed or allowlisted.

---

## 10. Upgrade Notes

1. Close Revit.
2. Install the new MSI package for your Revit version.
3. Restart Revit.
4. Start the DeepBim MCP connection from the Add-Ins tab.
5. For ChatGPT Online MCP mode, restart `cloudflared` and register the new tunnel URL at `/connect`.

---

## 11. Support

When reporting an issue, include:

- Revit version.
- Windows version.
- Plugin version.
- Connection mode: ChatGPT Online MCP or Local MCP Server.
- MCP client name and version.
- Installation method.
- Steps to reproduce.
- Screenshots or logs.

---

## 12. Project Information

| Item | Value |
|------|-------|
| Project | DeepBim Revit MCP Plugin |
| Version | v3.0.0 |
| Author | Nguyen Ngoc Due |
| GitHub | `https://github.com/nguyenngocdue` |
| License | MIT |

---

**DeepBim - Simplifying AI integration with Revit**
erything:

👉 Install → Connect → Use  

- No setup  
- No cloning  
- No complexity  

---

**DeepBim – Simplifying AI integration with Revit 🚀**
