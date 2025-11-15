namespace HeadlessIdeMcp.Core.ProcessExecution;

/// <summary>
/// Result of a command execution
/// </summary>
public class ExecutionResult
{
    /// <summary>
    /// Standard output from the command
    /// </summary>
    public string Stdout { get; set; } = string.Empty;

    /// <summary>
    /// Standard error from the command
    /// </summary>
    public string Stderr { get; set; } = string.Empty;

    /// <summary>
    /// Exit code (0 = success, non-zero = error)
    /// </summary>
    public int ExitCode { get; set; }

    /// <summary>
    /// Whether the command execution timed out
    /// </summary>
    public bool TimedOut { get; set; }

    /// <summary>
    /// Time taken to execute the command
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }
}
