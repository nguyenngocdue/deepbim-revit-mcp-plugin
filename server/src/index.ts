#!/usr/bin/env node
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { registerTools } from "./tools/register.js";

const server = new McpServer({
  name: "deepbim-mcp-server",
  version: "1.0.0",
});

async function main() {
  await registerTools(server);

  const transport = new StdioServerTransport();
  await server.connect(transport);
  console.error("DeepBim-MCP Server started successfully.");
}

main().catch((error) => {
  console.error("Error starting DeepBim-MCP Server:", error);
  process.exit(1);
});
