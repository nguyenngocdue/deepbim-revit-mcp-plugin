using System;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;

namespace DeepBimMCPTools
{
    [Transaction(TransactionMode.Manual)]
    public class GeometryCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                return Result.Succeeded;
            } 
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("GeometryCommand failed", ex.Message);
                return Result.Failed;
            }
         }

    }
}
