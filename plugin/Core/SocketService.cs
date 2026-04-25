using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Models.JsonRPC;
using RevitMCPSDK.API.Interfaces;
using revit_mcp_plugin.Configuration;
using revit_mcp_plugin.Utils;

namespace revit_mcp_plugin.Core
{
    public class SocketService
    {
        private static SocketService _instance;
        private TcpListener _listener;
        private Thread _listenerThread;
        private HttpListener _httpListener;
        private Thread _httpListenerThread;
        private bool _isRunning;
        private int _port = 8080;
        private UIApplication _uiApp;
        private ICommandRegistry _commandRegistry;
        private ILogger _logger;
        private CommandExecutor _commandExecutor;

        public static SocketService Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new SocketService();
                return _instance;
            }
        }

        private SocketService()
        {
            _commandRegistry = new RevitCommandRegistry();
            _logger = new Logger();
        }

        public bool IsRunning => _isRunning;
        public int Port => _port;
        private bool _isInitialized;

        private const int DEFAULT_PORT = 8080;
        private const int MAX_PORT = 8099;
        private const int HTTP_PORT = 9080;
        private static readonly string PortFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DeepBim-MCP", "mcp-port.txt");

        public void Initialize(UIApplication uiApp)
        {
            if (_isInitialized) return;

            _uiApp = uiApp;

            ExternalEventManager.Instance.Initialize(uiApp, _logger);

            _commandExecutor = new CommandExecutor(_commandRegistry, _logger);

            ConfigurationManager configManager = new ConfigurationManager(_logger);
            configManager.LoadConfiguration();

            CommandManager commandManager = new CommandManager(
                _commandRegistry, _logger, configManager, _uiApp);
            commandManager.LoadCommands();

            _isInitialized = true;
            _logger.Info("Socket service initialized.");
        }

        public void Start()
        {
            if (_isRunning) return;

            int lastPort = TryReadLastPort();
            foreach (int port in GetPortOrder(lastPort))
            {
                try
                {
                    _listener = new TcpListener(IPAddress.Any, port);
                    _listener.Start();
                    _port = port;
                    _isRunning = true;

                    _listenerThread = new Thread(ListenForClients)
                    {
                        IsBackground = true
                    };
                    _listenerThread.Start();

                    SaveLastPort(port);
                    _logger.Info($"TCP server listening on port {_port}");

                    // Also start HTTP listener on fixed port 9080
                    StartHttp(HTTP_PORT);

                    return;
                }
                catch (SocketException)
                {
                    try { _listener?.Stop(); _listener?.Server?.Close(); } catch { }
                    _listener = null;
                }
            }

            throw new Exception($"No available port in range {DEFAULT_PORT}-{MAX_PORT}. All are in use.");
        }

        /// <summary>Start HTTP listener for remote access (e.g. via Cloudflare Tunnel).</summary>
        public void StartHttp(int httpPort)
        {
            try
            {
                // Use TcpListener to avoid HttpListener permission issues on Windows
                var tcpHttp = new TcpListener(IPAddress.Any, httpPort);
                tcpHttp.Start();

                var thread = new Thread(() => AcceptHttpConnections(tcpHttp))
                {
                    IsBackground = true,
                    Name = "HttpRawListener"
                };
                thread.Start();

                _logger.Info($"HTTP server listening on port {httpPort}");
            }
            catch (Exception ex)
            {
                _logger.Warning($"Failed to start HTTP server on port {httpPort}: {ex.Message}");
            }
        }

        private void AcceptHttpConnections(TcpListener tcpHttp)
        {
            while (_isRunning)
            {
                try
                {
                    var client = tcpHttp.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(_ => HandleRawHttpClient(client));
                }
                catch { break; }
            }
        }

        private void HandleRawHttpClient(TcpClient client)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    // Read HTTP request
                    var requestBytes = new System.Collections.Generic.List<byte>();
                    var buf = new byte[4096];
                    string requestText = "";

                    // Read until we have full headers + body
                    int totalRead = 0;
                    while (true)
                    {
                        int n = stream.Read(buf, 0, buf.Length);
                        if (n == 0) break;
                        totalRead += n;
                        requestText += Encoding.UTF8.GetString(buf, 0, n);
                        // Check if we have full HTTP request
                        int headerEnd = requestText.IndexOf("\r\n\r\n");
                        if (headerEnd < 0) continue;
                        // Parse Content-Length
                        int contentLength = 0;
                        foreach (var line in requestText.Substring(0, headerEnd).Split('\n'))
                        {
                            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                            {
                                int.TryParse(line.Split(':')[1].Trim(), out contentLength);
                            }
                        }
                        string body = requestText.Substring(headerEnd + 4);
                        if (body.Length >= contentLength) break;
                    }

                    // Parse method and path
                    string firstLine = requestText.Split('\n')[0].Trim();
                    string method = firstLine.Split(' ')[0];
                    string path = firstLine.Split(' ').Length > 1 ? firstLine.Split(' ')[1] : "/";

                    string corsHeaders =
                        "Access-Control-Allow-Origin: *\r\n" +
                        "Access-Control-Allow-Methods: POST, GET, OPTIONS\r\n" +
                        "Access-Control-Allow-Headers: Content-Type\r\n";

                    string responseBody;
                    int statusCode;

                    if (method == "OPTIONS")
                    {
                        SendHttpResponse(stream, 204, "", corsHeaders);
                        return;
                    }
                    else if (method == "GET")
                    {
                        responseBody = $"{{\"status\":\"running\",\"port\":{_port},\"httpPort\":{HTTP_PORT}}}";
                        statusCode = 200;
                    }
                    else if (method == "POST")
                    {
                        int bodyStart = requestText.IndexOf("\r\n\r\n") + 4;
                        string jsonBody = requestText.Substring(bodyStart);
                        responseBody = ProcessJsonRPCRequest(jsonBody);
                        statusCode = 200;
                    }
                    else
                    {
                        responseBody = "{\"error\":\"Method not allowed\"}";
                        statusCode = 405;
                    }

                    SendHttpResponse(stream, statusCode, responseBody, corsHeaders);
                }
            }
            catch { }
        }

        private void SendHttpResponse(NetworkStream stream, int statusCode, string body, string extraHeaders = "")
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
            string statusText = statusCode == 200 ? "OK" : statusCode == 204 ? "No Content" : statusCode == 405 ? "Method Not Allowed" : "Error";
            string response =
                $"HTTP/1.1 {statusCode} {statusText}\r\n" +
                "Content-Type: application/json; charset=utf-8\r\n" +
                $"Content-Length: {bodyBytes.Length}\r\n" +
                "Connection: close\r\n" +
                extraHeaders +
                "\r\n";
            byte[] headerBytes = Encoding.UTF8.GetBytes(response);
            stream.Write(headerBytes, 0, headerBytes.Length);
            if (bodyBytes.Length > 0)
                stream.Write(bodyBytes, 0, bodyBytes.Length);
        }

        /// <summary>Try last used port first, then 8080, 8081, ... 8099.</summary>
        private static IEnumerable<int> GetPortOrder(int lastPort)
        {
            if (lastPort >= DEFAULT_PORT && lastPort <= MAX_PORT)
                yield return lastPort;
            for (int p = DEFAULT_PORT; p <= MAX_PORT; p++)
            {
                if (p == lastPort) continue;
                yield return p;
            }
        }

        /// <summary>Returns the last port used (saved when server was started). 0 if none saved.</summary>
        public static int GetLastUsedPort()
        {
            return TryReadLastPort();
        }

        private static int TryReadLastPort()
        {
            try
            {
                if (File.Exists(PortFilePath))
                {
                    string s = File.ReadAllText(PortFilePath).Trim();
                    if (int.TryParse(s, out int p) && p >= DEFAULT_PORT && p <= MAX_PORT)
                        return p;
                }
            }
            catch { }
            return 0;
        }

        private static void SaveLastPort(int port)
        {
            try
            {
                string dir = Path.GetDirectoryName(PortFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(PortFilePath, port.ToString());
            }
            catch { }
        }

        public void Stop()
        {
            _isRunning = false;

            try { _listener?.Stop(); } catch { }
            try { _listener?.Server?.Close(); _listener?.Server?.Dispose(); } catch { }
            _listener = null;

            try { _httpListener?.Stop(); } catch { }
            _httpListener = null;

            if (_listenerThread != null && _listenerThread.IsAlive)
                _listenerThread.Join(2000);
            _listenerThread = null;

            if (_httpListenerThread != null && _httpListenerThread.IsAlive)
                _httpListenerThread.Join(2000);
            _httpListenerThread = null;
        }

        private void ListenForClients()
        {
            try
            {
                while (_isRunning)
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    Thread clientThread = new Thread(HandleClientCommunication)
                    {
                        IsBackground = true
                    };
                    clientThread.Start(client);
                }
            }
            catch (SocketException) { }
            catch (Exception) { }
        }

        private void HandleClientCommunication(object clientObj)
        {
            TcpClient tcpClient = (TcpClient)clientObj;
            NetworkStream stream = tcpClient.GetStream();

            try
            {
                byte[] buffer = new byte[8192];

                while (_isRunning && tcpClient.Connected)
                {
                    int bytesRead = 0;
                    try
                    {
                        bytesRead = stream.Read(buffer, 0, buffer.Length);
                    }
                    catch (IOException)
                    {
                        break;
                    }

                    if (bytesRead == 0) break;

                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    System.Diagnostics.Trace.WriteLine($"Received message: {message}");

                    string response = ProcessJsonRPCRequest(message);

                    byte[] responseData = Encoding.UTF8.GetBytes(response);
                    stream.Write(responseData, 0, responseData.Length);
                }
            }
            catch (Exception) { }
            finally
            {
                tcpClient.Close();
            }
        }

        private string ProcessJsonRPCRequest(string requestJson)
        {
            JsonRPCRequest request;

            try
            {
                request = JsonConvert.DeserializeObject<JsonRPCRequest>(requestJson);

                if (request == null || !request.IsValid())
                {
                    return CreateErrorResponse(null, JsonRPCErrorCodes.InvalidRequest, "Invalid JSON-RPC request");
                }

                if (!_commandRegistry.TryGetCommand(request.Method, out var command))
                {
                    return CreateErrorResponse(request.Id, JsonRPCErrorCodes.MethodNotFound,
                        $"Method '{request.Method}' not found");
                }

                try
                {
                    object result = command.Execute(request.GetParamsObject(), request.Id);
                    return CreateSuccessResponse(request.Id, result);
                }
                catch (Exception ex)
                {
                    return CreateErrorResponse(request.Id, JsonRPCErrorCodes.InternalError, ex.Message);
                }
            }
            catch (JsonException)
            {
                return CreateErrorResponse(null, JsonRPCErrorCodes.ParseError, "Invalid JSON");
            }
            catch (Exception ex)
            {
                return CreateErrorResponse(null, JsonRPCErrorCodes.InternalError, $"Internal error: {ex.Message}");
            }
        }

        private string CreateSuccessResponse(string id, object result)
        {
            var response = new JsonRPCSuccessResponse
            {
                Id = id,
                Result = result is JToken jToken ? jToken : JToken.FromObject(result)
            };
            return response.ToJson();
        }

        private string CreateErrorResponse(string id, int code, string message, object data = null)
        {
            var response = new JsonRPCErrorResponse
            {
                Id = id,
                Error = new JsonRPCError
                {
                    Code = code,
                    Message = message,
                    Data = data != null ? JToken.FromObject(data) : null
                }
            };
            return response.ToJson();
        }
    }
}
