namespace HeadlessIdeMcp.Core;

/// <summary>
/// Service for file system operations
/// </summary>
public interface IFileSystemService
{
    /// <summary>
    /// Checks if a file exists at the specified path
    /// </summary>
    /// <param name="filePath">The path to check</param>
    /// <returns>True if the file exists, false otherwise</returns>
    bool FileExists(string filePath);
}
