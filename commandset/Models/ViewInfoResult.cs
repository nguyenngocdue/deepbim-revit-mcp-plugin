namespace RevitMCPCommandSet.Models;

public class ViewInfoResult
{
    public int Id { get; set; }
    public string UniqueId { get; set; }
    public string Name { get; set; }
    public string ViewType { get; set; }
    public bool IsTemplate { get; set; }
    public int Scale { get; set; }
    public string DetailLevel { get; set; }
}
