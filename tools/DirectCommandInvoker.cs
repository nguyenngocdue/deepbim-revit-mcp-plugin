using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Interfaces;
using System;
using System.IO;
using System.Reflection;

namespace DeepBimMCPTools
{
    /// <summary>
    /// Gọi trực tiếp command trong RevitMCPCommandSet (không cần MCP server).
    /// Dùng cho panel Tools để test từng chức năng.
    /// </summary>
    public static class DirectCommandInvoker
    {
        public static object Invoke(UIApplication uiApp, string methodName, JObject parameters = null)
        {
            if (uiApp == null) throw new ArgumentNullException(nameof(uiApp));
            if (string.IsNullOrEmpty(methodName)) throw new ArgumentException("methodName is required.", nameof(methodName));

            string pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string version = uiApp.Application.VersionNumber;
            string assemblyPath = Path.Combine(pluginDir, "Commands", "RevitMCPCommandSet", version, "RevitMCPCommandSet.dll");

            if (!File.Exists(assemblyPath))
                throw new FileNotFoundException($"Command set not found: {assemblyPath}. Build the solution and load add-in from plugin output.");

            Assembly asm = Assembly.LoadFrom(assemblyPath);
            parameters ??= new JObject();
            string requestId = Guid.NewGuid().ToString("N");

            foreach (Type type in asm.GetTypes())
            {
                if (type.IsInterface || type.IsAbstract) continue;
                if (!typeof(IRevitCommand).IsAssignableFrom(type)) continue;

                IRevitCommand command = null;
                try
                {
                    ConstructorInfo ctor = type.GetConstructor(new[] { typeof(UIApplication) });
                    if (ctor != null)
                        command = (IRevitCommand)ctor.Invoke(new object[] { uiApp });
                    else
                        command = (IRevitCommand)Activator.CreateInstance(type);
                }
                catch { continue; }

                if (command?.CommandName != methodName) continue;

                return command.Execute(parameters, requestId);
            }

            throw new InvalidOperationException($"Command '{methodName}' not found in RevitMCPCommandSet.");
        }
    }
}
