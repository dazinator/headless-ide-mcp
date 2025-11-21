namespace DevBuddy.Server.Data.Models;

public class Edge
{
    public int Id { get; set; }
    public int FromNodeId { get; set; }
    public int ToNodeId { get; set; }
    public int EdgeTypeId { get; set; }
    public string? MetadataJson { get; set; }
    
    // Navigation properties
    public Node FromNode { get; set; } = null!;
    public Node ToNode { get; set; } = null!;
    public EdgeType EdgeType { get; set; } = null!;
}
