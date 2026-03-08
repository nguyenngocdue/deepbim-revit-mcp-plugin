using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.DataExtraction;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.DataExtraction
{
    public class GetSheetExportablePropertiesCommand : ExternalEventCommandBase
    {
        private GetSheetExportablePropertiesEventHandler _handler => (GetSheetExportablePropertiesEventHandler)Handler;

        public override string CommandName => "get_sheet_exportable_properties";

        public GetSheetExportablePropertiesCommand(UIApplication uiApp)
            : base(new GetSheetExportablePropertiesEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            if (!RaiseAndWaitForCompletion(15000))
                throw new System.TimeoutException("get_sheet_exportable_properties timed out.");
            return new
            {
                success = true,
                propertyNames = _handler.Result?.Select(p => p.Name).ToList() ?? new List<string>()
            };
        }
    }
}
