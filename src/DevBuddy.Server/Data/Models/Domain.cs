namespace DevBuddy.Server.Data.Models;

public class Domain
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ParentDomainId { get; set; }
    
    // Navigation properties
    public Domain? ParentDomain { get; set; }
    public ICollection<Domain> ChildDomains { get; set; } = new List<Domain>();
    public ICollection<NodeType> NodeTypes { get; set; } = new List<NodeType>();
    public ICollection<Node> Nodes { get; set; } = new List<Node>();
}
