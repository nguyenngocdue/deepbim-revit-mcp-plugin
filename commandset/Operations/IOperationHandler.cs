using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models;

namespace RevitMCPCommandSet.Operations
{
    /// <summary>
    /// Implement this interface to add a new primitive operation to apply_operations.
    /// One class = one "op" value. The registry discovers all implementations automatically via reflection.
    /// </summary>
    public interface IOperationHandler
    {
        /// <summary>Value of the "op" field this handler responds to (e.g. "create_level").</summary>
        string OpName { get; }

        /// <summary>
        /// Fields that must be present in the operation JSON.
        /// Checked before Execute() is called — missing fields cause a structured validation error.
        /// </summary>
        string[] RequiredFields { get; }

        /// <summary>Execute the operation. Always called inside an active Transaction.</summary>
        OperationResult Execute(Document doc, JObject op);
    }
}
