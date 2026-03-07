using Newtonsoft.Json;

namespace revit_mcp_plugin.Configuration
{
    public class DeveloperInfo
    {
        [JsonProperty("name")]
        public string Name { get; set; } = "";

        [JsonProperty("email")]
        public string Email { get; set; } = "";

        [JsonProperty("website")]
        public string Website { get; set; } = "";

        [JsonProperty("organization")]
        public string Organization { get; set; } = "";
    }
}
