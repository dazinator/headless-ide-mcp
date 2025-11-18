using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace DevBuddy.Core.ProcessExecution;

/// <summary>
/// Implementation of command execution service with security controls
/// </summary>
public class CommandExecutionService : ICommandExecutionService
{
    private readonly string _basePath;
    private readonly CommandExecutionOptions _options;
    private readonly ILogger<CommandExecutionService>? _logger;

    /// <summary>
    /// Create a new CommandExecutionService
    /// </summary>
    /// <param name="basePath">The base path where commands can be executed</param>
    /// <param name="options">Optional configuration options</param>
    /// <param name="logger">Optional logger for audit logging</param>
    public CommandExecutionService(
        string basePath, 
        CommandExecutionOptions? options = null,
        ILogger<CommandExecutionService>? logger = null)
    {
        _basePath = basePath ?? throw new ArgumentNullException(nameof(basePath));
        _options = options ?? new CommandExecutionOptions();
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ExecutionResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.Command))
            throw new ArgumentException("Command cannot be empty", nameof(request));

        // Generate correlation ID if not provided
        var correlationId = request.CorrelationId ?? Guid.NewGuid().ToString();

        try
        {
            // Validate command against allowlist/denylist
            ValidateCommand(request.Command);

            // Validate timeout
            if (request.TimeoutSeconds <= 0 || request.TimeoutSeconds > _options.MaxTimeoutSeconds)
                throw new ArgumentException($"Timeout must be between 1 and {_options.MaxTimeoutSeconds} seconds");

            var workingDir = GetWorkingDirectory(request.WorkingDirectory);
            ValidateWorkingDirectory(workingDir);

            // Audit log: Command execution started
            LogCommandExecution(correlationId, request, "Started", null);

            var result = await ExecuteCommandInternalAsync(request, workingDir, correlationId, cancellationToken);

            // Audit log: Command execution completed
            LogCommandExecution(correlationId, request, "Completed", result);

            return result;
        }
        catch (Exception ex)
        {
            // Audit log: Command execution failed
            LogCommandExecution(correlationId, request, "Failed", null, ex);

            // Rethrow - validation exceptions already have sanitized messages if configured
            throw;
        }
    }

    private void ValidateCommand(string command)
    {
        // Check denylist first
        if (_options.DeniedCommands.Any(denied => 
            command.Equals(denied, StringComparison.OrdinalIgnoreCase)))
        {
            var message = _options.SanitizeErrorMessages
                ? "Command not permitted"
                : $"Command '{command}' is in the denylist";
            throw new UnauthorizedAccessException(message);
        }

        // Check allowlist if configured
        if (_options.AllowedCommands != null && _options.AllowedCommands.Any())
        {
            if (!_options.AllowedCommands.Any(allowed => 
                command.Equals(allowed, StringComparison.OrdinalIgnoreCase)))
            {
                var message = _options.SanitizeErrorMessages
                    ? "Command not permitted"
                    : $"Command '{command}' is not in the allowlist";
                throw new UnauthorizedAccessException(message);
            }
        }
    }

    private async Task<ExecutionResult> ExecuteCommandInternalAsync(
        ExecutionRequest request,
        string workingDir,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var result = new ExecutionResult { CorrelationId = correlationId };
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
            
            // Sanitize error message if configured
            result.Stderr = _options.SanitizeErrorMessages
                ? "Command execution failed"
                : $"Execution failed: {ex.Message}";
            
            result.ExecutionTime = DateTime.UtcNow - startTime;
            return result;
        }
    }

    private void LogCommandExecution(
        string correlationId,
        ExecutionRequest request,
        string status,
        ExecutionResult? result,
        Exception? exception = null)
    {
        if (!_options.EnableAuditLogging || _logger == null)
            return;

        // Redact sensitive data from arguments
        var sanitizedArgs = request.Arguments?.Select(RedactSensitiveData).ToArray();

        var logData = new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId,
            ["Timestamp"] = DateTime.UtcNow,
            ["Command"] = request.Command,
            ["Arguments"] = sanitizedArgs,
            ["User"] = request.User ?? "unknown",
            ["WorkingDirectory"] = RedactPath(request.WorkingDirectory),
            ["TimeoutSeconds"] = request.TimeoutSeconds,
            ["Status"] = status
        };

        if (result != null)
        {
            logData["ExitCode"] = result.ExitCode;
            logData["ExecutionTimeMs"] = result.ExecutionTime.TotalMilliseconds;
            logData["TimedOut"] = result.TimedOut;
            logData["StdoutLength"] = result.Stdout.Length;
            logData["StderrLength"] = result.Stderr.Length;
        }

        var logLevel = status switch
        {
            "Failed" => LogLevel.Error,
            "Completed" when result?.ExitCode != 0 => LogLevel.Warning,
            _ => LogLevel.Information
        };

        if (exception != null)
        {
            _logger.Log(logLevel, exception, 
                "Command execution {Status}: {Command} (CorrelationId: {CorrelationId})",
                status, request.Command, correlationId);
        }
        else
        {
            _logger.Log(logLevel,
                "Command execution {Status}: {Command} {Arguments} (CorrelationId: {CorrelationId}, User: {User}, ExitCode: {ExitCode}, Duration: {DurationMs}ms)",
                status, request.Command, string.Join(" ", sanitizedArgs ?? Array.Empty<string>()), 
                correlationId, request.User ?? "unknown", result?.ExitCode, result?.ExecutionTime.TotalMilliseconds);
        }
    }

    private string RedactSensitiveData(string data)
    {
        // Redact common sensitive patterns
        var patterns = new[]
        {
            @"password[=:]\s*\S+",
            @"token[=:]\s*\S+",
            @"key[=:]\s*\S+",
            @"secret[=:]\s*\S+"
        };

        var redacted = data;
        foreach (var pattern in patterns)
        {
            redacted = System.Text.RegularExpressions.Regex.Replace(
                redacted, pattern, "$1=***REDACTED***", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return redacted;
    }

    private string? RedactPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !_options.SanitizeErrorMessages)
            return path;

        // Only return relative path without full system paths
        return Path.GetFileName(path);
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
            var message = _options.SanitizeErrorMessages
                ? "Access to the requested path is not permitted"
                : $"Working directory '{workingDir}' is not within allowed paths: {string.Join(", ", allowedPaths)}";
            
            throw new UnauthorizedAccessException(message);
        }

        // Validate directory exists
        if (!Directory.Exists(normalizedWorkingDir))
        {
            var message = _options.SanitizeErrorMessages
                ? "The requested directory does not exist"
                : $"Working directory '{workingDir}' does not exist";
            
            throw new DirectoryNotFoundException(message);
        }
    }
}
