using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ClosedXML.Excel;
using RevitMCPSDK.API.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RevitMCPCommandSet.Services.DataExtraction
{
    public class ExportSheetsToExcelEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        public string OutputPath { get; set; }
        public List<string> PropertyNames { get; set; }
        public bool Success { get; private set; }
        public string Message { get; private set; }
        public int SheetCount { get; private set; }

        public bool WaitForCompletion(int timeoutMilliseconds = 60000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public void Execute(UIApplication app)
        {
            try
            {
                var doc = app.ActiveUIDocument?.Document;
                if (doc == null)
                {
                    Success = false;
                    Message = "No active document.";
                    return;
                }

                if (string.IsNullOrWhiteSpace(OutputPath) || PropertyNames == null || PropertyNames.Count == 0)
                {
                    Success = false;
                    Message = "Output path and at least one property name are required.";
                    return;
                }

                var sheets = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Sheets)
                    .WhereElementIsNotElementType()
                    .ToElements()
                    .Cast<ViewSheet>()
                    .OrderBy(s => s.SheetNumber)
                    .ToList();

                var cols = new List<string> { "Sheet Number", "Sheet Name" };
                cols.AddRange(PropertyNames);

                using (var workbook = new XLWorkbook())
                {
                    var ws = workbook.Worksheets.Add("Sheets");
                    for (int c = 0; c < cols.Count; c++)
                        ws.Cell(1, c + 1).Value = cols[c];
                    ws.Row(1).Style.Font.Bold = true;

                    int row = 2;
                    foreach (ViewSheet sheet in sheets)
                    {
                        ws.Cell(row, 1).Value = sheet.SheetNumber ?? "";
                        ws.Cell(row, 2).Value = sheet.Name ?? "";
                        int col = 3;
foreach (string paramName in PropertyNames)
                            {
                                var p = sheet.LookupParameter(paramName)
                                    ?? sheet.GetOrderedParameters().FirstOrDefault(pr => pr?.Definition?.Name == paramName);
                            if (p != null)
                            {
                                if (p.StorageType == StorageType.String)
                                    ws.Cell(row, col).Value = p.AsString() ?? "";
                                else if (p.StorageType == StorageType.Integer)
                                    ws.Cell(row, col).Value = p.AsInteger();
                                else if (p.StorageType == StorageType.Double)
                                    ws.Cell(row, col).Value = p.AsDouble();
                                else if (p.StorageType == StorageType.ElementId)
                                {
                                    var id = p.AsElementId();
                                    if (id != null && id != ElementId.InvalidElementId)
                                        ws.Cell(row, col).Value = doc.GetElement(id)?.Name ?? id.ToString();
                                    else
                                        ws.Cell(row, col).Value = "";
                                }
                                else
                                    ws.Cell(row, col).Value = p.AsValueString() ?? "";
                            }
                            col++;
                        }
                        row++;
                    }

                    ws.Columns().AdjustToContents();
                    string dir = Path.GetDirectoryName(OutputPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    workbook.SaveAs(OutputPath);
                }

                SheetCount = sheets.Count;
                Success = true;
                Message = $"Exported {sheets.Count} sheets to {OutputPath}";
            }
            catch (Exception ex)
            {
                Success = false;
                Message = ex.Message;
                SheetCount = 0;
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        public string GetName() => "Export Sheets to Excel";
    }
}
