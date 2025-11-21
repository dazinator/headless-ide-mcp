namespace DevBuddy.Server.Services;

public interface IFileExplorerService
{
    /// <summary>
    /// Lists files and directories at the specified path
    /// </summary>
    Task<FileExplorerItem[]> ListDirectoryAsync(string path);
    
    /// <summary>
    /// Gets information about a specific file
    /// </summary>
    Task<FileInfo?> GetFileInfoAsync(string path);
    
    /// <summary>
    /// Reads file contents as bytes
    /// </summary>
    Task<byte[]> ReadFileAsync(string path);
    
    /// <summary>
    /// Gets the base path for file exploration
    /// </summary>
    string GetBasePath();
    
    /// <summary>
    /// Checks if a directory at the specified path is a git repository
    /// </summary>
    bool IsGitRepository(string path);
}

public class FileExplorerItem
{
    public required string Name { get; set; }
    public required string Path { get; set; }
    public required string RelativePath { get; set; }
    public required bool IsDirectory { get; set; }
    public long Size { get; set; }
    public DateTime LastModified { get; set; }
    public string? Extension { get; set; }
    public bool IsGitRepository { get; set; }
}
