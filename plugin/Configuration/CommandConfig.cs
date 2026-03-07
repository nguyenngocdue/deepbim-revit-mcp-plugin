using Newtonsoft.Json;

namespace revit_mcp_plugin.Configuration
{
    public class CommandConfig
    {
        [JsonProperty("commandName")]
        public string CommandName { get; set; }

        [JsonProperty("assemblyPath")]
        public string AssemblyPath { get; set; }

        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonProperty("supportedRevitVersions")]
        public string[] SupportedRevitVersions { get; set; } = new string[0];

        [JsonProperty("description")]
        public string Description { get; set; } = "";

        [JsonProperty("developer")]
        public DeveloperInfo Developer { get; set; } = new DeveloperInfo();
    }
}
