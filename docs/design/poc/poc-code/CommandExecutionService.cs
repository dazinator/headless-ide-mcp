using System.Diagnostics;
using System.Text;

namespace HeadlessIdeMcp.Core.ProcessExecution;

/// <summary>
/// Result of a command execution
/// </summary>
public class ExecutionResult
{
    public string Stdout { get; set; } = string.Empty;
    public string Stderr { get; set; } = string.Empty;
    public int ExitCode { get; set; }
    public bool TimedOut { get; set; }
    public TimeSpan ExecutionTime { get; set; }
}

/// <summary>
/// Request for command execution
/// </summary>
public class ExecutionRequest
{
    public string Command { get; set; } = string.Empty;
    public string[]? Arguments { get; set; }
    public string? WorkingDirectory { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public Dictionary<string, string>? EnvironmentVariables { get; set; }
}

/// <summary>
/// Service for executing shell commands safely in a sandboxed environment
/// POC 1: Process Execution Validation
/// </summary>
public interface ICommandExecutionService
{
    Task<ExecutionResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of command execution service
/// </summary>
public class CommandExecutionService : ICommandExecutionService
{
    private readonly string _basePath;
    private readonly CommandExecutionOptions _options;

    public CommandExecutionService(string basePath, CommandExecutionOptions? options = null)
    {
        _basePath = basePath ?? throw new ArgumentNullException(nameof(basePath));
        _options = options ?? new CommandExecutionOptions();
    }

    public async Task<ExecutionResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.Command))
            throw new ArgumentException("Command cannot be empty", nameof(request));

        // Validate timeout
        if (request.TimeoutSeconds <= 0 || request.TimeoutSeconds > _options.MaxTimeoutSeconds)
            throw new ArgumentException($"Timeout must be between 1 and {_options.MaxTimeoutSeconds} seconds");

        var workingDir = GetWorkingDirectory(request.WorkingDirectory);
        ValidateWorkingDirectory(workingDir);

        var result = new ExecutionResult();
        var startTime = DateTime.UtcNow;

        var processStartInfo = new ProcessStartInfo
        {
            FileName = request.Command,
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false, // Critical for security: no shell, direct process spawn
            CreateNoWindow = true
        };

        // Add arguments
        if (request.Arguments != null)
        {
            foreach (var arg in request.Arguments)
            {
                processStartInfo.ArgumentList.Add(arg);
            }
        }

        // Add environment variables
        if (request.EnvironmentVariables != null)
        {
            foreach (var kvp in request.EnvironmentVariables)
            {
                processStartInfo.Environment[kvp.Key] = kvp.Value;
            }
        }

        using var process = new Process { StartInfo = processStartInfo };
        
        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();

        // Capture stdout
        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                lock (stdoutBuilder)
                {
                    stdoutBuilder.AppendLine(e.Data);
                }
            }
        };

        // Capture stderr
        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                lock (stderrBuilder)
                {
                    stderrBuilder.AppendLine(e.Data);
                }
            }
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait for process to exit or timeout
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(request.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

            try
            {
                await process.WaitForExitAsync(linkedCts.Token);
                result.ExitCode = process.ExitCode;
                result.TimedOut = false;
            }
            catch (OperationCanceledException)
            {
                // Timeout or cancellation occurred
                if (!process.HasExited)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                        result.TimedOut = true;
                        result.ExitCode = -1;
                    }
                    catch
                    {
                        // Process may have already exited
                    }
                }

                // Re-throw if it was user cancellation, not timeout
                if (cancellationToken.IsCancellationRequested && !timeoutCts.Token.IsCancellationRequested)
                {
                    throw;
                }
            }

            // Give a moment for output to flush
            await Task.Delay(100, CancellationToken.None);

            result.Stdout = stdoutBuilder.ToString();
            result.Stderr = stderrBuilder.ToString();
            result.ExecutionTime = DateTime.UtcNow - startTime;

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            result.ExitCode = -1;
            result.Stderr = $"Execution failed: {ex.Message}";
            result.ExecutionTime = DateTime.UtcNow - startTime;
            return result;
        }
    }

    private string GetWorkingDirectory(string? requestedWorkingDir)
    {
        if (string.IsNullOrWhiteSpace(requestedWorkingDir))
        {
            return _basePath;
        }

        // If absolute path, validate it's within allowed paths
        if (Path.IsPathRooted(requestedWorkingDir))
        {
            return requestedWorkingDir;
        }

        // Combine with base path
        return Path.Combine(_basePath, requestedWorkingDir);
    }

    private void ValidateWorkingDirectory(string workingDir)
    {
        // Normalize paths for comparison
        var normalizedWorkingDir = Path.GetFullPath(workingDir);
        var normalizedBasePath = Path.GetFullPath(_basePath);

        // Check if working directory is within allowed paths
        var allowedPaths = _options.AllowedPaths.Select(p => Path.GetFullPath(p)).ToList();
        
        // Add base path to allowed paths
        if (!allowedPaths.Contains(normalizedBasePath))
        {
            allowedPaths.Add(normalizedBasePath);
        }

        bool isAllowed = allowedPaths.Any(allowedPath =>
            normalizedWorkingDir.StartsWith(allowedPath, StringComparison.OrdinalIgnoreCase));

        if (!isAllowed)
        {
            throw new UnauthorizedAccessException(
                $"Working directory '{workingDir}' is not within allowed paths: {string.Join(", ", allowedPaths)}");
        }

        // Validate directory exists
        if (!Directory.Exists(normalizedWorkingDir))
        {
            throw new DirectoryNotFoundException($"Working directory '{workingDir}' does not exist");
        }
    }
}

/// <summary>
/// Configuration options for command execution
/// </summary>
public class CommandExecutionOptions
{
    /// <summary>
    /// Maximum allowed timeout in seconds
    /// </summary>
    public int MaxTimeoutSeconds { get; set; } = 300; // 5 minutes max

    /// <summary>
    /// Allowed paths where commands can be executed
    /// Default: only base path and /tmp
    /// </summary>
    public List<string> AllowedPaths { get; set; } = new() { "/tmp" };

    /// <summary>
    /// Optional: Command allowlist for additional security
    /// If empty, all commands allowed
    /// </summary>
    public List<string>? AllowedCommands { get; set; }

    /// <summary>
    /// Optional: Command denylist for security
    /// </summary>
    public List<string> DeniedCommands { get; set; } = new()
    {
        "rm", "dd", "mkfs", "fdisk" // Dangerous filesystem commands
    };
}
