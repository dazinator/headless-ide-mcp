using HeadlessIdeMcp.Core.ProcessExecution;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace HeadlessIdeMcp.Server.Tools;

/// <summary>
/// POC 4: MCP Shell Execution Tools
/// Demonstrates the shell.execute and shell.executeJson tools from the design
/// </summary>
[McpServerToolType]
public class ShellTools
{
    private readonly ICommandExecutionService _executionService;

    public ShellTools(ICommandExecutionService executionService)
    {
        _executionService = executionService ?? throw new ArgumentNullException(nameof(executionService));
    }

    /// <summary>
    /// Execute a shell command and return stdout, stderr, and exit code
    /// </summary>
    [McpServerTool("shell_execute")]
    [Description("Execute a CLI command in a sandboxed environment. Returns stdout, stderr, and exit code.")]
    public async Task<ShellExecuteResponse> ExecuteAsync(
        [Description("The command to execute (e.g., 'dotnet', 'rg', 'jq')")] 
        string command,
        
        [Description("Command arguments as array (e.g., ['--version'] for 'dotnet --version')")] 
        string[]? arguments = null,
        
        [Description("Working directory for command execution (relative to workspace or absolute)")] 
        string? workingDirectory = null,
        
        [Description("Timeout in seconds (default: 30, max: 300)")] 
        int timeoutSeconds = 30)
    {
        var request = new ExecutionRequest
        {
            Command = command,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            TimeoutSeconds = timeoutSeconds
        };

        var result = await _executionService.ExecuteAsync(request);

        return new ShellExecuteResponse
        {
            Stdout = result.Stdout,
            Stderr = result.Stderr,
            ExitCode = result.ExitCode,
            TimedOut = result.TimedOut,
            ExecutionTimeMs = (int)result.ExecutionTime.TotalMilliseconds
        };
    }

    /// <summary>
    /// Execute a command that returns JSON and parse the result
    /// </summary>
    [McpServerTool("shell_execute_json")]
    [Description("Execute a CLI command that returns JSON output. Automatically parses the JSON response.")]
    public async Task<ShellExecuteJsonResponse> ExecuteJsonAsync(
        [Description("The command to execute (e.g., 'dotnet', 'jq')")] 
        string command,
        
        [Description("Command arguments as array")] 
        string[]? arguments = null,
        
        [Description("Working directory for command execution")] 
        string? workingDirectory = null,
        
        [Description("Timeout in seconds (default: 30, max: 300)")] 
        int timeoutSeconds = 30)
    {
        var request = new ExecutionRequest
        {
            Command = command,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            TimeoutSeconds = timeoutSeconds
        };

        var result = await _executionService.ExecuteAsync(request);

        JsonNode? parsedJson = null;
        string? parseError = null;

        if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.Stdout))
        {
            try
            {
                parsedJson = JsonNode.Parse(result.Stdout);
            }
            catch (JsonException ex)
            {
                parseError = $"Failed to parse JSON: {ex.Message}";
            }
        }

        return new ShellExecuteJsonResponse
        {
            Json = parsedJson,
            ParseError = parseError,
            Stderr = result.Stderr,
            ExitCode = result.ExitCode,
            TimedOut = result.TimedOut,
            ExecutionTimeMs = (int)result.ExecutionTime.TotalMilliseconds
        };
    }

    /// <summary>
    /// Get information about available CLI tools in the container
    /// </summary>
    [McpServerTool("shell_get_available_tools")]
    [Description("Get a list of available CLI tools in the container environment")]
    public async Task<AvailableToolsResponse> GetAvailableToolsAsync()
    {
        var tools = new List<ToolInfo>();
        var toolsToCheck = new[]
        {
            ("dotnet", new[] { "--version" }, ".NET SDK"),
            ("rg", new[] { "--version" }, "ripgrep - fast text search"),
            ("jq", new[] { "--version" }, "jq - JSON processor"),
            ("tree", new[] { "--version" }, "tree - directory visualization"),
            ("git", new[] { "--version" }, "git - version control"),
            ("bash", new[] { "--version" }, "bash - shell"),
            ("curl", new[] { "--version" }, "curl - data transfer tool"),
            ("find", new[] { "--version" }, "find - file search utility"),
        };

        foreach (var (cmd, args, description) in toolsToCheck)
        {
            try
            {
                var request = new ExecutionRequest
                {
                    Command = cmd,
                    Arguments = args,
                    TimeoutSeconds = 5
                };

                var result = await _executionService.ExecuteAsync(request);
                
                tools.Add(new ToolInfo
                {
                    Name = cmd,
                    Description = description,
                    Available = result.ExitCode == 0,
                    Version = result.ExitCode == 0 ? result.Stdout.Split('\n')[0].Trim() : null
                });
            }
            catch
            {
                tools.Add(new ToolInfo
                {
                    Name = cmd,
                    Description = description,
                    Available = false
                });
            }
        }

        return new AvailableToolsResponse
        {
            Tools = tools.ToArray(),
            WorkspacePath = Environment.GetEnvironmentVariable("CODE_BASE_PATH") ?? "/workspace"
        };
    }
}

/// <summary>
/// Response from shell.execute
/// </summary>
public class ShellExecuteResponse
{
    [Description("Standard output from the command")]
    public string Stdout { get; set; } = string.Empty;

    [Description("Standard error from the command")]
    public string Stderr { get; set; } = string.Empty;

    [Description("Exit code (0 = success, non-zero = error)")]
    public int ExitCode { get; set; }

    [Description("Whether the command timed out")]
    public bool TimedOut { get; set; }

    [Description("Execution time in milliseconds")]
    public int ExecutionTimeMs { get; set; }
}

/// <summary>
/// Response from shell.executeJson
/// </summary>
public class ShellExecuteJsonResponse
{
    [Description("Parsed JSON output from the command")]
    public JsonNode? Json { get; set; }

    [Description("Error message if JSON parsing failed")]
    public string? ParseError { get; set; }

    [Description("Standard error from the command")]
    public string Stderr { get; set; } = string.Empty;

    [Description("Exit code (0 = success, non-zero = error)")]
    public int ExitCode { get; set; }

    [Description("Whether the command timed out")]
    public bool TimedOut { get; set; }

    [Description("Execution time in milliseconds")]
    public int ExecutionTimeMs { get; set; }
}

/// <summary>
/// Information about available tools
/// </summary>
public class AvailableToolsResponse
{
    [Description("List of CLI tools available in the container")]
    public ToolInfo[] Tools { get; set; } = Array.Empty<ToolInfo>();

    [Description("Base workspace path where code is mounted")]
    public string WorkspacePath { get; set; } = string.Empty;
}

/// <summary>
/// Information about a single CLI tool
/// </summary>
public class ToolInfo
{
    [Description("Tool command name")]
    public string Name { get; set; } = string.Empty;

    [Description("Tool description")]
    public string Description { get; set; } = string.Empty;

    [Description("Whether the tool is available")]
    public bool Available { get; set; }

    [Description("Tool version string (if available)")]
    public string? Version { get; set; }
}
