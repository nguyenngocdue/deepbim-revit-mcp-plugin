namespace RevitMCPCommandSet.Models;

public class AIResult<T>
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public T Response { get; set; }
}
