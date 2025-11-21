namespace DevBuddy.Server.Data.Models;

public class NodeType
{
    public int Id { get; set; }
    public int DomainId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    // Navigation properties
    public Domain Domain { get; set; } = null!;
    public ICollection<Node> Nodes { get; set; } = new List<Node>();
}
