using HeadlessIdeMcp.Core.ProcessExecution;
using Shouldly;

namespace HeadlessIdeMcp.IntegrationTests.ProcessExecution;

/// <summary>
/// Integration tests for CommandExecutionService
/// These tests run against actual processes with no mocked dependencies
/// </summary>
public class CommandExecutionServiceIntegrationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly CommandExecutionService _sut;

    public CommandExecutionServiceIntegrationTests()
    {
        // Create a temporary test directory
        _testDirectory = Path.Combine(Path.GetTempPath(), $"mcp-exec-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        // Create some test files
        File.WriteAllText(Path.Combine(_testDirectory, "test.txt"), "Hello World");
        
        _sut = new CommandExecutionService(_testDirectory);
    }

    [Fact]
    public async Task ExecuteAsync_WithSimpleCommand_ReturnsSuccessResult()
    {
        // Arrange
        var request = new ExecutionRequest
        {
            Command = "echo",
            Arguments = new[] { "Hello World" },
            TimeoutSeconds = 5
        };

        // Act
        var result = await _sut.ExecuteAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.ExitCode.ShouldBe(0);
        result.TimedOut.ShouldBeFalse();
        result.Stdout.ShouldContain("Hello World");
    }

    [Fact]
    public async Task ExecuteAsync_WithDotnetVersion_ReturnsVersionInfo()
    {
        // Arrange
        var request = new ExecutionRequest
        {
            Command = "dotnet",
            Arguments = new[] { "--version" },
            TimeoutSeconds = 10
        };

        // Act
        var result = await _sut.ExecuteAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.ExitCode.ShouldBe(0);
        result.TimedOut.ShouldBeFalse();
        result.Stdout.ShouldNotBeEmpty();
        result.ExecutionTime.ShouldBeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task ExecuteAsync_WithWorkingDirectory_ExecutesInCorrectDirectory()
    {
        // Arrange
        var request = new ExecutionRequest
        {
            Command = "pwd",
            WorkingDirectory = _testDirectory,
            TimeoutSeconds = 5
        };

        // Act
        var result = await _sut.ExecuteAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.ExitCode.ShouldBe(0);
        result.Stdout.Trim().ShouldBe(_testDirectory);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidCommand_ReturnsErrorResult()
    {
        // Arrange
        var request = new ExecutionRequest
        {
            Command = "nonexistentcommand12345",
            TimeoutSeconds = 5
        };

        // Act
        var result = await _sut.ExecuteAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.ExitCode.ShouldBe(-1);
        result.Stderr.ShouldContain("Execution failed");
    }

    [Fact]
    public async Task ExecuteAsync_WithShortTimeout_TimesOut()
    {
        // Arrange - sleep command that will timeout
        var request = new ExecutionRequest
        {
            Command = "sleep",
            Arguments = new[] { "10" },
            TimeoutSeconds = 1
        };

        // Act
        var result = await _sut.ExecuteAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.TimedOut.ShouldBeTrue();
        result.ExitCode.ShouldBe(-1);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidWorkingDirectory_ThrowsException()
    {
        // Arrange - use a subdirectory of test directory (which is allowed) that doesn't exist
        var nonExistentSubDir = Path.Combine(_testDirectory, "nonexistent");
        var request = new ExecutionRequest
        {
            Command = "echo",
            Arguments = new[] { "test" },
            WorkingDirectory = nonExistentSubDir,
            TimeoutSeconds = 5
        };

        // Act & Assert
        await Should.ThrowAsync<DirectoryNotFoundException>(async () =>
        {
            await _sut.ExecuteAsync(request);
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithUnauthorizedPath_ThrowsException()
    {
        // Arrange - Try to execute in /etc which is not in allowed paths
        var request = new ExecutionRequest
        {
            Command = "echo",
            Arguments = new[] { "test" },
            WorkingDirectory = "/etc",
            TimeoutSeconds = 5
        };

        // Act & Assert
        await Should.ThrowAsync<UnauthorizedAccessException>(async () =>
        {
            await _sut.ExecuteAsync(request);
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithEnvironmentVariables_SetsVariables()
    {
        // Arrange
        var request = new ExecutionRequest
        {
            Command = "printenv",
            Arguments = new[] { "TEST_VAR" },
            EnvironmentVariables = new Dictionary<string, string>
            {
                { "TEST_VAR", "TestValue123" }
            },
            TimeoutSeconds = 5
        };

        // Act
        var result = await _sut.ExecuteAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.ExitCode.ShouldBe(0);
        result.Stdout.ShouldContain("TestValue123");
    }

    [Fact]
    public async Task ExecuteAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
        {
            await _sut.ExecuteAsync(null!);
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyCommand_ThrowsArgumentException()
    {
        // Arrange
        var request = new ExecutionRequest
        {
            Command = "",
            TimeoutSeconds = 5
        };

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(async () =>
        {
            await _sut.ExecuteAsync(request);
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidTimeout_ThrowsArgumentException()
    {
        // Arrange
        var request = new ExecutionRequest
        {
            Command = "echo",
            TimeoutSeconds = 0
        };

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(async () =>
        {
            await _sut.ExecuteAsync(request);
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithTimeoutExceedingMax_ThrowsArgumentException()
    {
        // Arrange
        var request = new ExecutionRequest
        {
            Command = "echo",
            TimeoutSeconds = 400 // Exceeds default max of 300
        };

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(async () =>
        {
            await _sut.ExecuteAsync(request);
        });
    }

    public void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }
}
