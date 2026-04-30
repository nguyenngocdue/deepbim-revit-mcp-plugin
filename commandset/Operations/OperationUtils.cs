using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;

namespace RevitMCPCommandSet.Operations
{
    internal static class OperationUtils
    {
        internal static double MmToFeet(double mm) => mm / 304.8;

        internal static double FeetToMm(double feet) => feet * 304.8;

        internal static XYZ ToXyzFromMm(JToken token)
        {
            double[] values = token.ToObject<double[]>()
                ?? throw new ArgumentException("Point must be [x, y, z] in mm.");
            if (values.Length != 3)
                throw new ArgumentException("Point must be [x, y, z] in mm.");
            return new XYZ(MmToFeet(values[0]), MmToFeet(values[1]), MmToFeet(values[2]));
        }

        internal static int GetElementId(Element element)
        {
#if REVIT2024_OR_GREATER
            return (int)element.Id.Value;
#else
            return element.Id.IntegerValue;
#endif
        }
    }
}
