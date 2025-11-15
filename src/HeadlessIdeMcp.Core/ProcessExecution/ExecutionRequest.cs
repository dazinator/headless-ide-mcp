namespace HeadlessIdeMcp.Core.ProcessExecution;

/// <summary>
/// Request for command execution
/// </summary>
public class ExecutionRequest
{
    /// <summary>
    /// The command to execute (e.g., 'dotnet', 'rg', 'jq')
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// Command arguments as array
    /// </summary>
    public string[]? Arguments { get; set; }

    /// <summary>
    /// Working directory for command execution (relative to workspace or absolute)
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Timeout in seconds (default: 30, max: configurable via options)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Optional environment variables to set for the command
    /// </summary>
    public Dictionary<string, string>? EnvironmentVariables { get; set; }
}
