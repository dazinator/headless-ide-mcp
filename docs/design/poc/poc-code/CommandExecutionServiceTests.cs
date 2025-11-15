using System.Diagnostics;
using Xunit;

namespace HeadlessIdeMcp.Core.ProcessExecution.Tests;

/// <summary>
/// POC 1: Process Execution Validation Tests
/// These tests validate the critical assumption that .NET can reliably execute
/// child processes in a containerized environment.
/// </summary>
public class CommandExecutionServiceTests
{
    private readonly string _testBasePath;

    public CommandExecutionServiceTests()
    {
        _testBasePath = Path.GetTempPath();
    }

    [Fact]
    public async Task Execute_SimpleCommand_Success()
    {
        // Arrange
        var service = new CommandExecutionService(_testBasePath);
        var request = new ExecutionRequest
        {
            Command = "echo",
            Arguments = new[] { "Hello World" }
        };

        // Act
        var result = await service.ExecuteAsync(request);

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Hello World", result.Stdout);
        Assert.False(result.TimedOut);
    }

    [Fact]
    public async Task Execute_CommandWithStderr_CapturesBothStreams()
    {
        // Arrange
        var service = new CommandExecutionService(_testBasePath);
        // This command writes to both stdout and stderr
        var request = new ExecutionRequest
        {
            Command = "sh",
            Arguments = new[] { "-c", "echo 'stdout message' && echo 'stderr message' >&2" }
        };

        // Act
        var result = await service.ExecuteAsync(request);

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("stdout message", result.Stdout);
        Assert.Contains("stderr message", result.Stderr);
    }

    [Fact]
    public async Task Execute_NonZeroExitCode_ReturnsCorrectCode()
    {
        // Arrange
        var service = new CommandExecutionService(_testBasePath);
        var request = new ExecutionRequest
        {
            Command = "sh",
            Arguments = new[] { "-c", "exit 42" }
        };

        // Act
        var result = await service.ExecuteAsync(request);

        // Assert
        Assert.Equal(42, result.ExitCode);
    }

    [Fact]
    public async Task Execute_TimeoutEnforcement_KillsProcess()
    {
        // Arrange
        var service = new CommandExecutionService(_testBasePath);
        var request = new ExecutionRequest
        {
            Command = "sleep",
            Arguments = new[] { "10" },
            TimeoutSeconds = 1 // 1 second timeout
        };

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await service.ExecuteAsync(request);
        stopwatch.Stop();

        // Assert
        Assert.True(result.TimedOut);
        Assert.Equal(-1, result.ExitCode);
        Assert.True(stopwatch.Elapsed.TotalSeconds < 2, "Should timeout within ~1 second");
    }

    [Fact]
    public async Task Execute_LargeOutput_CapturesAll()
    {
        // Arrange
        var service = new CommandExecutionService(_testBasePath);
        // Generate ~10KB of output
        var request = new ExecutionRequest
        {
            Command = "sh",
            Arguments = new[] { "-c", "for i in {1..500}; do echo 'Line $i with some text to make it longer'; done" }
        };

        // Act
        var result = await service.ExecuteAsync(request);

        // Assert
        Assert.Equal(0, result.ExitCode);
        var lineCount = result.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
        Assert.True(lineCount >= 500, $"Expected at least 500 lines, got {lineCount}");
    }

    [Fact]
    public async Task Execute_WorkingDirectory_UsesCorrectPath()
    {
        // Arrange
        var service = new CommandExecutionService(_testBasePath);
        var request = new ExecutionRequest
        {
            Command = "pwd",
            WorkingDirectory = _testBasePath
        };

        // Act
        var result = await service.ExecuteAsync(request);

        // Assert
        Assert.Equal(0, result.ExitCode);
        var outputPath = result.Stdout.Trim();
        Assert.True(outputPath.Contains(_testBasePath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Execute_InvalidTimeout_ThrowsException()
    {
        // Arrange
        var service = new CommandExecutionService(_testBasePath);
        var request = new ExecutionRequest
        {
            Command = "echo",
            Arguments = new[] { "test" },
            TimeoutSeconds = 0 // Invalid
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => service.ExecuteAsync(request));
    }

    [Fact]
    public async Task Execute_PathTraversal_ThrowsUnauthorizedAccess()
    {
        // Arrange
        var service = new CommandExecutionService(_testBasePath);
        var request = new ExecutionRequest
        {
            Command = "ls",
            WorkingDirectory = "/etc" // Outside allowed paths
        };

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ExecuteAsync(request));
    }

    [Fact]
    public async Task Execute_AllowedPath_Succeeds()
    {
        // Arrange
        var options = new CommandExecutionOptions
        {
            AllowedPaths = new List<string> { "/tmp", _testBasePath }
        };
        var service = new CommandExecutionService(_testBasePath, options);
        var request = new ExecutionRequest
        {
            Command = "ls",
            WorkingDirectory = "/tmp"
        };

        // Act
        var result = await service.ExecuteAsync(request);

        // Assert
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task Execute_NonExistentCommand_ReturnsError()
    {
        // Arrange
        var service = new CommandExecutionService(_testBasePath);
        var request = new ExecutionRequest
        {
            Command = "this-command-does-not-exist-12345"
        };

        // Act
        var result = await service.ExecuteAsync(request);

        // Assert
        Assert.Equal(-1, result.ExitCode);
        Assert.Contains("failed", result.Stderr, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Execute_CancellationToken_StopsExecution()
    {
        // Arrange
        var service = new CommandExecutionService(_testBasePath);
        var request = new ExecutionRequest
        {
            Command = "sleep",
            Arguments = new[] { "30" },
            TimeoutSeconds = 60
        };

        var cts = new CancellationTokenSource();
        cts.CancelAfter(500); // Cancel after 500ms

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => 
            service.ExecuteAsync(request, cts.Token));
    }

    [Fact]
    public async Task Execute_EnvironmentVariables_PassedToProcess()
    {
        // Arrange
        var service = new CommandExecutionService(_testBasePath);
        var request = new ExecutionRequest
        {
            Command = "sh",
            Arguments = new[] { "-c", "echo $TEST_VAR" },
            EnvironmentVariables = new Dictionary<string, string>
            {
                { "TEST_VAR", "TestValue123" }
            }
        };

        // Act
        var result = await service.ExecuteAsync(request);

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("TestValue123", result.Stdout);
    }

    [Fact]
    public async Task Execute_ConcurrentCommands_AllSucceed()
    {
        // Arrange
        var service = new CommandExecutionService(_testBasePath);
        var tasks = new List<Task<ExecutionResult>>();

        // Act - Execute 10 commands concurrently
        for (int i = 0; i < 10; i++)
        {
            var index = i;
            var request = new ExecutionRequest
            {
                Command = "echo",
                Arguments = new[] { $"Message {index}" }
            };
            tasks.Add(service.ExecuteAsync(request));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results, r => Assert.Equal(0, r.ExitCode));
        Assert.All(results, r => Assert.False(r.TimedOut));
        Assert.Equal(10, results.Length);
    }

    [Fact]
    public async Task Execute_ExecutionTime_Tracked()
    {
        // Arrange
        var service = new CommandExecutionService(_testBasePath);
        var request = new ExecutionRequest
        {
            Command = "sleep",
            Arguments = new[] { "1" }
        };

        // Act
        var result = await service.ExecuteAsync(request);

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.ExecutionTime.TotalSeconds >= 1);
        Assert.True(result.ExecutionTime.TotalSeconds < 2);
    }
}

/// <summary>
/// POC 1: Integration Tests with Real CLI Tools
/// These validate that common CLI tools work as expected
/// </summary>
public class CLIToolsIntegrationTests
{
    private readonly string _testBasePath;

    public CLIToolsIntegrationTests()
    {
        _testBasePath = Path.GetTempPath();
    }

    [Fact]
    public async Task Execute_DotnetVersion_Success()
    {
        // Arrange
        var service = new CommandExecutionService(_testBasePath);
        var request = new ExecutionRequest
        {
            Command = "dotnet",
            Arguments = new[] { "--version" }
        };

        // Act
        var result = await service.ExecuteAsync(request);

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.NotEmpty(result.Stdout);
    }

    [Fact]
    public async Task Execute_EchoCommand_Works()
    {
        // Arrange
        var service = new CommandExecutionService(_testBasePath);
        var request = new ExecutionRequest
        {
            Command = "echo",
            Arguments = new[] { "Test message" }
        };

        // Act
        var result = await service.ExecuteAsync(request);

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Test message", result.Stdout);
    }

    [Fact]
    public async Task Execute_DateCommand_ReturnsOutput()
    {
        // Arrange
        var service = new CommandExecutionService(_testBasePath);
        var request = new ExecutionRequest
        {
            Command = "date"
        };

        // Act
        var result = await service.ExecuteAsync(request);

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.NotEmpty(result.Stdout);
    }
}
