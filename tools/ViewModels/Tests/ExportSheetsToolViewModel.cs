using Newtonsoft.Json.Linq;
using System.IO;

namespace DeepBimMCPTools
{
    public sealed class ExportSheetsToolViewModel : ToolViewModelBase
    {
        public ExportSheetsToolViewModel(IToolExecutionService executionService) : base(executionService)
        {
            OutputPath = Path.Combine(Path.GetTempPath(), "DeepBimMCP_SheetsExport_Test.xlsx");
        }

        public override string MethodName => "export_sheets_to_excel";
        public override string DialogTitle => "Test: export_sheets_to_excel";

        public string OutputPath { get; set; }
        public string[] PropertyNames { get; set; } = { "Sheet Number", "Sheet Name" };

        protected override JObject BuildParameters()
        {
            return new JObject
            {
                ["outputPath"] = OutputPath,
                ["propertyNames"] = new JArray(PropertyNames)
            };
        }

        protected override string FormatResult(object result)
        {
            string text = base.FormatResult(result);
            return text + "\n\nFile: " + OutputPath;
        }
    }
}
