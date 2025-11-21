namespace DevBuddy.Server.Data.Models;

public enum ContentStorageType
{
    Embedded,
    External
}

public class Node
{
    public int Id { get; set; }
    public int DomainId { get; set; }
    public int NodeTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? MetadataJson { get; set; }
    public ContentStorageType ContentStorageType { get; set; }
    public string? ExternalUri { get; set; }
    
    // Navigation properties
    public Domain Domain { get; set; } = null!;
    public NodeType NodeType { get; set; } = null!;
    public Document? Document { get; set; }
    public ICollection<Edge> OutgoingEdges { get; set; } = new List<Edge>();
    public ICollection<Edge> IncomingEdges { get; set; } = new List<Edge>();
}
