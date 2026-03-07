import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

export async function registerTools(server: McpServer) {
  const __filename = fileURLToPath(import.meta.url);
  const __dirname = path.dirname(__filename);

  const files = fs.readdirSync(__dirname);

  const toolFiles = files.filter(
    (file) =>
      (file.endsWith(".ts") || file.endsWith(".js")) &&
      file !== "index.ts" &&
      file !== "index.js" &&
      file !== "register.ts" &&
      file !== "register.js"
  );

  for (const file of toolFiles) {
    try {
      const importPath = `./${file.replace(/\.(ts|js)$/, ".js")}`;
      const module = await import(importPath);

      const registerFunctionName = Object.keys(module).find(
        (key) => key.startsWith("register") && typeof module[key] === "function"
      );

      if (registerFunctionName) {
        module[registerFunctionName](server);
        console.error(`Registered tool: ${file}`);
      }
    } catch (error) {
      console.error(`Error registering tool ${file}:`, error);
    }
  }
}
