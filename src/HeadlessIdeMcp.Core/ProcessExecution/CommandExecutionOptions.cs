namespace HeadlessIdeMcp.Core.ProcessExecution;

/// <summary>
/// Configuration options for command execution
/// </summary>
public class CommandExecutionOptions
{
    /// <summary>
    /// Maximum allowed timeout in seconds
    /// Default: 300 (5 minutes)
    /// </summary>
    public int MaxTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Allowed paths where commands can be executed
    /// Default: only base path and /tmp
    /// </summary>
    public List<string> AllowedPaths { get; set; } = new() { "/tmp" };

    /// <summary>
    /// Optional: Command allowlist for additional security
    /// If empty, all commands allowed (subject to denylist)
    /// </summary>
    public List<string>? AllowedCommands { get; set; }

    /// <summary>
    /// Optional: Command denylist for security
    /// Default: Dangerous filesystem commands
    /// </summary>
    public List<string> DeniedCommands { get; set; } = new()
    {
        "rm", "dd", "mkfs", "fdisk"
    };
}
