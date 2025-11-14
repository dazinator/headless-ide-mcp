namespace HeadlessIdeMcp.Core;

/// <summary>
/// Implementation of file system operations
/// </summary>
public class FileSystemService : IFileSystemService
{
    private readonly string _basePath;

    public FileSystemService(string basePath)
    {
        _basePath = basePath ?? throw new ArgumentNullException(nameof(basePath));
    }

    public bool FileExists(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        // Combine with base path if relative path is provided
        var fullPath = Path.IsPathRooted(filePath) 
            ? filePath 
            : Path.Combine(_basePath, filePath);

        return File.Exists(fullPath);
    }
}
