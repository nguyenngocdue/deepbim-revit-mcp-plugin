namespace RevitMCPCommandSet.Models
{
    public class OperationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public int? ElementId { get; set; }

        public static OperationResult Ok(string message, int? elementId = null)
        {
            return new OperationResult
            {
                Success = true,
                Message = message,
                ElementId = elementId
            };
        }

        public static OperationResult Fail(string message)
        {
            return new OperationResult
            {
                Success = false,
                Message = message
            };
        }
    }
}
