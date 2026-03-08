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

            try
            {
                _listener?.Stop();
            }
            catch { }

            try
            {
                _listener?.Server?.Close();
                _listener?.Server?.Dispose();
            }
            catch { }

            _listener = null;

            if (_listenerThread != null && _listenerThread.IsAlive)
            {
                _listenerThread.Join(2000);
            }
            _listenerThread = null;
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
