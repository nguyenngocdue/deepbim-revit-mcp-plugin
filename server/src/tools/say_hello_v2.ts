import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerSayHelloV2Tool(server: McpServer) {
  server.tool(
    "say_hello_v2",
    "Display a greeting dialog in Revit using DevToolV2Commands business logic.",
    {
      message: z
        .string()
        .optional()
        .describe("Optional custom message to display. Defaults to 'Hello from V2!'"),
    },
    async (args) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("say_hello_v2", args);
        });

        return {
          content: [
            {
              type: "text",
              text: JSON.stringify(response, null, 2),
            },
          ],
        };
      } catch (error) {
        return {
          content: [
            {
              type: "text",
              text: `say_hello_v2 failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
