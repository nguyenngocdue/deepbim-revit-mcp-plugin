using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DeepBimMCPTools
{
    internal static class ToolResultFormatter
    {
        public static string Format(object result, int maxLength)
        {
            string text;
            if (result == null)
            {
                text = "(no result)";
            }
            else if (result is JToken token)
            {
                text = token.ToString(Formatting.Indented);
            }
            else
            {
                try
                {
                    text = JsonConvert.SerializeObject(result, Formatting.Indented);
                }
                catch
                {
                    text = result.ToString() ?? "(no result)";
                }
            }

            if (maxLength > 0 && text.Length > maxLength)
            {
                return text.Substring(0, maxLength) + "...";
            }

            return text;
        }
    }
}
