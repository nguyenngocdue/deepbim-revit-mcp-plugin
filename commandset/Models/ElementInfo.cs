using System.Collections.Generic;

namespace RevitMCPCommandSet.Models;

public class ElementInfo
{
    public int Id { get; set; }
    public string UniqueId { get; set; }
    public string Name { get; set; }
    public string Category { get; set; }
    public string FamilyName { get; set; }
    public string TypeName { get; set; }
    public Dictionary<string, string> Properties { get; set; } = new();
}
