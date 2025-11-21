namespace DevBuddy.Server.Services;

public class FileExplorerService : IFileExplorerService
{
    private readonly string _basePath;
    private readonly ILogger<FileExplorerService> _logger;

    public FileExplorerService(IConfiguration configuration, ILogger<FileExplorerService> logger)
    {
        _basePath = configuration["GitRepositoriesPath"] ?? "/git-repos";
        _logger = logger;
    }

    public string GetBasePath() => _basePath;

    public async Task<FileExplorerItem[]> ListDirectoryAsync(string path)
    {
        await Task.CompletedTask; // Keep method async for future I/O operations
        
        var fullPath = GetSafePath(path);
        
        if (!Directory.Exists(fullPath))
        {
            _logger.LogWarning("Directory does not exist: {Path}", fullPath);
            return Array.Empty<FileExplorerItem>();
        }

        try
        {
            var items = new List<FileExplorerItem>();
            var dirInfo = new DirectoryInfo(fullPath);

            // Add directories
            foreach (var dir in dirInfo.GetDirectories())
            {
                try
                {
                    var relativePath = GetRelativePath(dir.FullName);
                    items.Add(new FileExplorerItem
                    {
                        Name = dir.Name,
                        Path = dir.FullName,
                        RelativePath = relativePath,
                        IsDirectory = true,
                        Size = 0,
                        LastModified = dir.LastWriteTime,
                        Extension = null,
                        IsGitRepository = IsGitRepository(relativePath)
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error accessing directory: {DirName}", dir.Name);
                }
            }

            // Add files
            foreach (var file in dirInfo.GetFiles())
            {
                try
                {
                    items.Add(new FileExplorerItem
                    {
                        Name = file.Name,
                        Path = file.FullName,
                        RelativePath = GetRelativePath(file.FullName),
                        IsDirectory = false,
                        Size = file.Length,
                        LastModified = file.LastWriteTime,
                        Extension = file.Extension,
                        IsGitRepository = false
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error accessing file: {FileName}", file.Name);
                }
            }

            // Sort: directories first, then files, both alphabetically
            return items.OrderBy(i => !i.IsDirectory).ThenBy(i => i.Name).ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing directory: {Path}", fullPath);
            throw;
        }
    }

    public async Task<FileInfo?> GetFileInfoAsync(string path)
    {
        await Task.CompletedTask; // Keep method async for future I/O operations
        
        var fullPath = GetSafePath(path);
        
        if (!File.Exists(fullPath))
        {
            return null;
        }

        return new FileInfo(fullPath);
    }

    public async Task<byte[]> ReadFileAsync(string path)
    {
        var fullPath = GetSafePath(path);
        
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("File not found", path);
        }

        return await File.ReadAllBytesAsync(fullPath);
    }

    private string GetSafePath(string path)
    {
        // Normalize path separators
        path = path.Replace('\\', '/');
        
        // Remove leading slash if present
        if (path.StartsWith('/'))
        {
            path = path.Substring(1);
        }

        // Combine with base path
        var fullPath = string.IsNullOrEmpty(path) ? _basePath : Path.Combine(_basePath, path);
        
        // Normalize and get full path
        fullPath = Path.GetFullPath(fullPath);
        
        // Security check: ensure the resolved path is within base path
        if (!fullPath.StartsWith(_basePath, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException($"Access to path '{path}' is denied");
        }

        return fullPath;
    }

    public bool IsGitRepository(string path)
    {
        try
        {
            var fullPath = GetSafePath(path);
            if (!Directory.Exists(fullPath))
            {
                return false;
            }
            
            var gitDir = Path.Combine(fullPath, ".git");
            return Directory.Exists(gitDir);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking if path is git repository: {Path}", path);
            return false;
        }
    }

    private string GetRelativePath(string fullPath)
    {
        if (fullPath.StartsWith(_basePath, StringComparison.Ordinal))
        {
            var relativePath = fullPath.Substring(_basePath.Length);
            return relativePath.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        return fullPath;
    }
}
