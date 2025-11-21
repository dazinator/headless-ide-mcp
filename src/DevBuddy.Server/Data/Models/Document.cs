namespace DevBuddy.Server.Data.Models;

public class Document
{
    public int Id { get; set; }
    public int NodeId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string ContentType { get; set; } = "text/markdown";
    
    // Navigation properties
    public Node Node { get; set; } = null!;
}
