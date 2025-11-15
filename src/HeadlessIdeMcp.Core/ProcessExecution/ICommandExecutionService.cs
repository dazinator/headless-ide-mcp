namespace HeadlessIdeMcp.Core.ProcessExecution;

/// <summary>
/// Service for executing shell commands safely in a sandboxed environment
/// </summary>
public interface ICommandExecutionService
{
    /// <summary>
    /// Execute a command asynchronously with the specified parameters
    /// </summary>
    /// <param name="request">The execution request containing command, arguments, and options</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    /// <returns>The execution result containing stdout, stderr, exit code, and execution metadata</returns>
    Task<ExecutionResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default);
}
