using HeadlessIdeMcp.Core;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace HeadlessIdeMcp.Server;

/// <summary>
/// MCP tools for file system operations
/// </summary>
[McpServerToolType]
public class FileSystemTools
{
    private readonly IFileSystemService _fileSystemService;

    public FileSystemTools(IFileSystemService fileSystemService)
    {
        _fileSystemService = fileSystemService ?? throw new ArgumentNullException(nameof(fileSystemService));
    }

    /// <summary>
    /// Checks if a specific file exists in the code base
    /// </summary>
    /// <param name="fileName">The name of the file to check (can be relative or absolute path)</param>
    /// <returns>A message indicating whether the file exists</returns>
    [McpServerTool]
    [Description("Checks if a specific file exists in the code base")]
    public string CheckFileExists(
        [Description("The file path to check (can be relative to the code base or absolute)")] 
        string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "Error: fileName parameter is required";
        }

        bool exists = _fileSystemService.FileExists(fileName);
        return exists 
            ? $"File '{fileName}' exists" 
            : $"File '{fileName}' does not exist";
    }
}
