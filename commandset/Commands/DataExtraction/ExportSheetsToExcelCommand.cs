using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.DataExtraction;
using RevitMCPSDK.API.Base;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitMCPCommandSet.Commands.DataExtraction
{
    public class ExportSheetsToExcelCommand : ExternalEventCommandBase
    {
        private ExportSheetsToExcelEventHandler _handler => (ExportSheetsToExcelEventHandler)Handler;

        public override string CommandName => "export_sheets_to_excel";

        public ExportSheetsToExcelCommand(UIApplication uiApp)
            : base(new ExportSheetsToExcelEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            string outputPath = parameters?["outputPath"]?.ToString();
            var propertyNames = new List<string>();
            if (parameters?["propertyNames"] is JArray arr)
            {
                foreach (var t in arr)
                    if (t?.ToString() is string s && !string.IsNullOrWhiteSpace(s))
                        propertyNames.Add(s.Trim());
            }

            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("outputPath is required.");
            if (propertyNames.Count == 0)
                throw new ArgumentException("At least one property name is required in propertyNames.");

            _handler.OutputPath = outputPath;
            _handler.PropertyNames = propertyNames;

            if (!RaiseAndWaitForCompletion(60000))
                throw new TimeoutException("export_sheets_to_excel timed out.");

            return new
            {
                success = _handler.Success,
                message = _handler.Message,
                sheetCount = _handler.SheetCount,
                outputPath = outputPath
            };
        }
    }
}
