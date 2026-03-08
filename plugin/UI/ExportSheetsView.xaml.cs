using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ClosedXML.Excel;
using Microsoft.Win32;

namespace revit_mcp_plugin.UI
{
    public partial class ExportSheetsView : UserControl
    {
        private readonly UIApplication _uiApp;
        private List<SheetPropertyItem> _propertyItems;

        public ExportSheetsView(UIApplication uiApp)
        {
            _uiApp = uiApp ?? throw new ArgumentNullException(nameof(uiApp));
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _propertyItems = new List<SheetPropertyItem>();
            var doc = _uiApp.ActiveUIDocument?.Document;
            if (doc == null)
            {
                MessageBox.Show("No active document.", "Export Sheets", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var paramNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var sheets = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Sheets)
                .WhereElementIsNotElementType()
                .ToElements();

            foreach (Element el in sheets)
            {
                foreach (Parameter p in el.GetOrderedParameters())
                {
                    if (p?.Definition?.Name == null) continue;
                    paramNames.Add(p.Definition.Name);
                }
            }

            foreach (string name in paramNames.OrderBy(s => s))
                _propertyItems.Add(new SheetPropertyItem { Name = name, IsSelected = true });

            PropertiesItemsControl.ItemsSource = _propertyItems;
        }

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (_propertyItems == null) return;
            foreach (var item in _propertyItems) item.IsSelected = true;
        }

        private void DeselectAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (_propertyItems == null) return;
            foreach (var item in _propertyItems) item.IsSelected = false;
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*",
                DefaultExt = "xlsx",
                FileName = "SheetsExport.xlsx"
            };
            if (dlg.ShowDialog() == true)
                PathTextBox.Text = dlg.FileName;
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            string path = PathTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(path))
            {
                MessageBox.Show("Please choose an output file path.", "Export Sheets", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selected = _propertyItems?.Where(p => p.IsSelected).Select(p => p.Name).ToList();
            if (selected == null || selected.Count == 0)
            {
                MessageBox.Show("Select at least one property to export.", "Export Sheets", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var doc = _uiApp.ActiveUIDocument?.Document;
                if (doc == null) { MessageBox.Show("No active document."); return; }

                var viewSheets = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Sheets)
                    .WhereElementIsNotElementType()
                    .ToElements()
                    .Cast<ViewSheet>()
                    .OrderBy(s => s.SheetNumber)
                    .ToList();

                var cols = new List<string> { "Sheet Number", "Sheet Name" };
                cols.AddRange(selected);

                using (var workbook = new XLWorkbook())
                {
                    var ws = workbook.Worksheets.Add("Sheets");
                    for (int c = 0; c < cols.Count; c++)
                        ws.Cell(1, c + 1).Value = cols[c];
                    ws.Row(1).Style.Font.Bold = true;

                    int row = 2;
                    foreach (ViewSheet sheet in viewSheets)
                    {
                        ws.Cell(row, 1).Value = sheet.SheetNumber ?? "";
                        ws.Cell(row, 2).Value = sheet.Name ?? "";
                        int col = 3;
                        foreach (string paramName in selected)
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
                    string dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    workbook.SaveAs(path);
                }

                MessageBox.Show($"Exported {viewSheets.Count} sheets to:\n{path}", "Export Sheets", MessageBoxButton.OK, MessageBoxImage.Information);
                Window.GetWindow(this)?.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed:\n{ex.Message}", "Export Sheets", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) => Window.GetWindow(this)?.Close();
    }

    public class SheetPropertyItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        public string Name { get; set; }
        public bool IsSelected { get => _isSelected; set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); } }
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
