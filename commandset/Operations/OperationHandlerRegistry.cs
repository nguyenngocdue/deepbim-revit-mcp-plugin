using System.Reflection;

namespace RevitMCPCommandSet.Operations
{
    /// <summary>
    /// Auto-discovers all IOperationHandler implementations in this assembly at startup.
    /// To add a new operation: create a class implementing IOperationHandler — no registration needed.
    /// </summary>
    public static class OperationHandlerRegistry
    {
        private static readonly Dictionary<string, IOperationHandler> _handlers;

        static OperationHandlerRegistry()
        {
            _handlers = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t =>
                    typeof(IOperationHandler).IsAssignableFrom(t) &&
                    !t.IsInterface &&
                    !t.IsAbstract)
                .Select(t => (IOperationHandler)Activator.CreateInstance(t)!)
                .ToDictionary(h => h.OpName, StringComparer.OrdinalIgnoreCase);
        }

        public static bool TryGet(string opName, out IOperationHandler handler)
            => _handlers.TryGetValue(opName, out handler!);

        public static IEnumerable<string> KnownOps => _handlers.Keys.OrderBy(k => k);
    }
}
