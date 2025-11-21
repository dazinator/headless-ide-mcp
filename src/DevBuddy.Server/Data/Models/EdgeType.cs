namespace DevBuddy.Server.Data.Models;

public class EdgeType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    // Navigation properties
    public ICollection<Edge> Edges { get; set; } = new List<Edge>();
}
