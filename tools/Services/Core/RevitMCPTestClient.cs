using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json.Linq;

namespace DeepBimMCPTools
{
    /// <summary>
    /// Gửi JSON-RPC tới MCP server (Revit plugin) để test từng command.
    /// Cần bật Connect Server trước khi chạy test.
    /// </summary>
    public static class RevitMCPTestClient
    {
        private const int DefaultPortStart = 8080;
        private const int PortEnd = 8099;
        private static readonly string PortFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DeepBim-MCP", "mcp-port.txt");

        public static string SendCommand(string method, object parameters = null)
        {
            int port = TryReadPort();
            for (int p = port > 0 ? port : DefaultPortStart; p <= PortEnd; p++)
            {
                if (port > 0 && p != port) continue;
                try
                {
                    using var client = new TcpClient();
                    client.Connect("127.0.0.1", p);
                    client.ReceiveTimeout = 30000;
                    client.SendTimeout = 5000;
                    using var stream = client.GetStream();

                    var request = new JObject
                    {
                        ["jsonrpc"] = "2.0",
                        ["method"] = method,
                        ["params"] = parameters != null ? JObject.FromObject(parameters) : new JObject(),
                        ["id"] = Guid.NewGuid().ToString("N")
                    };
                    string requestJson = request.ToString();
                    byte[] requestBytes = Encoding.UTF8.GetBytes(requestJson);
                    stream.Write(requestBytes, 0, requestBytes.Length);
                    stream.Flush();

                    var responseBuffer = new byte[65536];
                    int read = stream.Read(responseBuffer, 0, responseBuffer.Length);
                    if (read <= 0) continue;
                    string responseJson = Encoding.UTF8.GetString(responseBuffer, 0, read);
                    return responseJson;
                }
                catch (SocketException) when (port <= 0)
                { continue; }
                catch (IOException) when (port <= 0)
                { continue; }
            }
            throw new InvalidOperationException(
                "Không kết nối được MCP server. Hãy bấm \"Connect Server\" trong panel Server trước, đợi trạng thái Running rồi chạy lại test.");
        }

        private static int TryReadPort()
        {
            try
            {
                if (File.Exists(PortFilePath))
                {
                    string s = File.ReadAllText(PortFilePath).Trim();
                    if (int.TryParse(s, out int p) && p >= DefaultPortStart && p <= PortEnd)
                        return p;
                }
            }
            catch { }
            return 0;
        }
    }
}
