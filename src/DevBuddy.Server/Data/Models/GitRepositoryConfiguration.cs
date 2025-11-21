namespace DevBuddy.Server.Data.Models;

public class GitRepositoryConfiguration
{
    public int Id { get; set; }
    
    public required string Name { get; set; }
    
    public string? RemoteUrl { get; set; }
    
    public required string LocalPath { get; set; }
    
    public CloneStatus CloneStatus { get; set; }
    
    public string? ErrorMessage { get; set; }
    
    public string? CurrentBranch { get; set; }
    
    public int CommitsAhead { get; set; }
    
    public int CommitsBehind { get; set; }
    
    public bool HasUncommittedChanges { get; set; }
    
    public DateTime? LastChecked { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime UpdatedAt { get; set; }
}

public enum CloneStatus
{
    NotCloned,
    Cloning,
    Cloned,
    Failed,
    Conflict
}
