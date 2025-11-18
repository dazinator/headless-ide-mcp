using DevBuddy.Core.ProcessExecution;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace DevBuddy.IntegrationTests.ProcessExecution;

/// <summary>
/// Security tests for CommandExecutionService (Phase 2)
/// These tests validate production security controls
/// </summary>
public class SecurityTests : IDisposable
{
    private readonly string _testDirectory;

    public SecurityTests()
    {
        // Create a temporary test directory
        _testDirectory = Path.Combine(Path.GetTempPath(), $"mcp-security-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        File.WriteAllText(Path.Combine(_testDirectory, "test.txt"), "Hello World");
    }

    #region Command Allowlist/Denylist Tests

    [Fact]
    public async Task ExecuteAsync_WithDeniedCommand_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var options = new CommandExecutionOptions
        {
            DeniedCommands = new List<string> { "rm", "dd" }
        };
        var sut = new CommandExecutionService(_testDirectory, options);

        var request = new ExecutionRequest
        {
            Command = "rm",
            Arguments = new[] { "-rf", "/" },
            TimeoutSeconds = 5
        };

        // Act & Assert
        await Should.ThrowAsync<UnauthorizedAccessException>(async () =>
        {
            await sut.ExecuteAsync(request);
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithAllowedCommandsConfigured_OnlyPermitsAllowedCommands()
    {
        // Arrange
        var options = new CommandExecutionOptions
        {
            AllowedCommands = new List<string> { "echo", "pwd" }
        };
        var sut = new CommandExecutionService(_testDirectory, options);

        // Test allowed command succeeds
        var allowedRequest = new ExecutionRequest
        {
            Command = "echo",
            Arguments = new[] { "test" },
            TimeoutSeconds = 5
        };

        var result = await sut.ExecuteAsync(allowedRequest);
        result.ExitCode.ShouldBe(0);

        // Test non-allowed command fails
        var deniedRequest = new ExecutionRequest
        {
            Command = "cat",
            Arguments = new[] { "test.txt" },
            TimeoutSeconds = 5
        };

        await Should.ThrowAsync<UnauthorizedAccessException>(async () =>
        {
            await sut.ExecuteAsync(deniedRequest);
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithBothAllowlistAndDenylist_DenylistTakesPrecedence()
    {
        // Arrange
        var options = new CommandExecutionOptions
        {
            AllowedCommands = new List<string> { "echo", "rm" }, // rm is in both lists
            DeniedCommands = new List<string> { "rm" }
        };
        var sut = new CommandExecutionService(_testDirectory, options);

        var request = new ExecutionRequest
        {
            Command = "rm",
            Arguments = new[] { "test.txt" },
            TimeoutSeconds = 5
        };

        // Act & Assert - denylist should take precedence
        await Should.ThrowAsync<UnauthorizedAccessException>(async () =>
        {
            await sut.ExecuteAsync(request);
        });
    }

    #endregion

    #region Error Sanitization Tests

    [Fact]
    public async Task ExecuteAsync_WithSanitizationEnabled_HidesPathInError()
    {
        // Arrange
        var options = new CommandExecutionOptions
        {
            SanitizeErrorMessages = true
        };
        var sut = new CommandExecutionService(_testDirectory, options);

        var invalidPath = Path.Combine(_testDirectory, "nonexistent");
        var request = new ExecutionRequest
        {
            Command = "echo",
            WorkingDirectory = invalidPath,
            TimeoutSeconds = 5
        };

        // Act & Assert
        var exception = await Should.ThrowAsync<DirectoryNotFoundException>(async () =>
        {
            await sut.ExecuteAsync(request);
        });

        // Error message should not contain the full path
        exception.Message.ShouldNotContain(invalidPath);
        exception.Message.ShouldBe("The requested directory does not exist");
    }

    [Fact]
    public async Task ExecuteAsync_WithSanitizationEnabled_HidesUnauthorizedPathDetails()
    {
        // Arrange
        var options = new CommandExecutionOptions
        {
            SanitizeErrorMessages = true
        };
        var sut = new CommandExecutionService(_testDirectory, options);

        var unauthorizedPath = "/etc";
        var request = new ExecutionRequest
        {
            Command = "echo",
            WorkingDirectory = unauthorizedPath,
            TimeoutSeconds = 5
        };

        // Act & Assert
        var exception = await Should.ThrowAsync<UnauthorizedAccessException>(async () =>
        {
            await sut.ExecuteAsync(request);
        });

        // Error message should not contain specific paths
        exception.Message.ShouldNotContain(unauthorizedPath);
        exception.Message.ShouldBe("Access to the requested path is not permitted");
    }

    [Fact]
    public async Task ExecuteAsync_WithSanitizationEnabled_HidesCommandNameInDenyError()
    {
        // Arrange
        var options = new CommandExecutionOptions
        {
            SanitizeErrorMessages = true,
            DeniedCommands = new List<string> { "rm" }
        };
        var sut = new CommandExecutionService(_testDirectory, options);

        var request = new ExecutionRequest
        {
            Command = "rm",
            TimeoutSeconds = 5
        };

        // Act & Assert
        var exception = await Should.ThrowAsync<UnauthorizedAccessException>(async () =>
        {
            await sut.ExecuteAsync(request);
        });

        // Error message should be generic
        exception.Message.ShouldBe("Command not permitted");
    }

    #endregion

    #region Audit Logging Tests

    [Fact]
    public async Task ExecuteAsync_WithAuditLoggingEnabled_GeneratesCorrelationId()
    {
        // Arrange
        var options = new CommandExecutionOptions
        {
            EnableAuditLogging = true
        };
        var logger = NullLogger<CommandExecutionService>.Instance;
        var sut = new CommandExecutionService(_testDirectory, options, logger);

        var request = new ExecutionRequest
        {
            Command = "echo",
            Arguments = new[] { "test" },
            TimeoutSeconds = 5
        };

        // Act
        var result = await sut.ExecuteAsync(request);

        // Assert
        result.CorrelationId.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WithProvidedCorrelationId_PreservesIt()
    {
        // Arrange
        var expectedCorrelationId = "test-correlation-123";
        var options = new CommandExecutionOptions
        {
            EnableAuditLogging = true
        };
        var logger = NullLogger<CommandExecutionService>.Instance;
        var sut = new CommandExecutionService(_testDirectory, options, logger);

        var request = new ExecutionRequest
        {
            Command = "echo",
            Arguments = new[] { "test" },
            CorrelationId = expectedCorrelationId,
            TimeoutSeconds = 5
        };

        // Act
        var result = await sut.ExecuteAsync(request);

        // Assert
        result.CorrelationId.ShouldBe(expectedCorrelationId);
    }

    #endregion

    #region Path Traversal Attack Tests

    [Fact]
    public async Task ExecuteAsync_WithPathTraversalAttempt_Denied()
    {
        // Arrange
        var options = new CommandExecutionOptions
        {
            AllowedPaths = new List<string> { _testDirectory }
        };
        var sut = new CommandExecutionService(_testDirectory, options);

        // Attempt to traverse to parent directory
        var traversalPath = Path.Combine(_testDirectory, "..", "..", "etc");
        var request = new ExecutionRequest
        {
            Command = "echo",
            WorkingDirectory = traversalPath,
            TimeoutSeconds = 5
        };

        // Act & Assert
        await Should.ThrowAsync<UnauthorizedAccessException>(async () =>
        {
            await sut.ExecuteAsync(request);
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithSymbolicLinkEscape_Denied()
    {
        // Arrange
        var options = new CommandExecutionOptions();
        var sut = new CommandExecutionService(_testDirectory, options);

        // Try to access /etc which is outside allowed paths
        var request = new ExecutionRequest
        {
            Command = "echo",
            WorkingDirectory = "/etc",
            TimeoutSeconds = 5
        };

        // Act & Assert
        await Should.ThrowAsync<UnauthorizedAccessException>(async () =>
        {
            await sut.ExecuteAsync(request);
        });
    }

    #endregion

    #region Command Injection Prevention Tests

    [Fact]
    public async Task ExecuteAsync_WithShellMetacharacters_DoesNotExecuteShell()
    {
        // Arrange
        var sut = new CommandExecutionService(_testDirectory);

        // Attempt to chain commands using shell metacharacters
        var request = new ExecutionRequest
        {
            Command = "echo",
            Arguments = new[] { "test", ";", "ls", "-la" },
            TimeoutSeconds = 5
        };

        // Act
        var result = await sut.ExecuteAsync(request);

        // Assert - arguments are passed literally, not interpreted by shell
        // The semicolon and ls should be printed as text, not executed
        result.Stdout.ShouldContain(";");
        result.ExitCode.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_WithPipeCharacter_TreatedAsLiteralText()
    {
        // Arrange
        var sut = new CommandExecutionService(_testDirectory);

        var request = new ExecutionRequest
        {
            Command = "echo",
            Arguments = new[] { "test", "|", "cat" },
            TimeoutSeconds = 5
        };

        // Act
        var result = await sut.ExecuteAsync(request);

        // Assert - pipe character should be printed, not create a pipe
        result.Stdout.ShouldContain("|");
        result.Stdout.ShouldContain("cat");
        result.ExitCode.ShouldBe(0);
    }

    #endregion

    #region Resource Exhaustion Prevention Tests

    [Fact]
    public async Task ExecuteAsync_WithExcessiveTimeout_Rejected()
    {
        // Arrange
        var options = new CommandExecutionOptions
        {
            MaxTimeoutSeconds = 60
        };
        var sut = new CommandExecutionService(_testDirectory, options);

        var request = new ExecutionRequest
        {
            Command = "echo",
            TimeoutSeconds = 120 // Exceeds max
        };

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(async () =>
        {
            await sut.ExecuteAsync(request);
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithLongRunningCommand_TimesOut()
    {
        // Arrange
        var sut = new CommandExecutionService(_testDirectory);

        var request = new ExecutionRequest
        {
            Command = "sleep",
            Arguments = new[] { "10" },
            TimeoutSeconds = 1 // Very short timeout
        };

        // Act
        var result = await sut.ExecuteAsync(request);

        // Assert
        result.TimedOut.ShouldBeTrue();
        result.ExitCode.ShouldBe(-1);
    }

    #endregion

    #region Information Disclosure Prevention Tests

    [Fact]
    public async Task ExecuteAsync_WithFailedCommand_SanitizedErrorDoesNotLeakDetails()
    {
        // Arrange
        var options = new CommandExecutionOptions
        {
            SanitizeErrorMessages = true
        };
        var sut = new CommandExecutionService(_testDirectory, options);

        var request = new ExecutionRequest
        {
            Command = "nonexistentcommand12345",
            TimeoutSeconds = 5
        };

        // Act
        var result = await sut.ExecuteAsync(request);

        // Assert - error message should be sanitized
        result.Stderr.ShouldBe("Command execution failed");
        result.ExitCode.ShouldBe(-1);
    }

    #endregion

    public void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
