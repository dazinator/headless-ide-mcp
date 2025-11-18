using DevBuddy.Core.ProcessExecution;
using DevBuddy.Server;
using Shouldly;

namespace DevBuddy.IntegrationTests.ProcessExecution;

/// <summary>
/// Integration tests for ShellTools MCP integration
/// These tests verify the MCP tools work correctly end-to-end
/// </summary>
public class ShellToolsIntegrationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly CommandExecutionService _executionService;
    private readonly ShellTools _sut;

    public ShellToolsIntegrationTests()
    {
        // Create a temporary test directory
        _testDirectory = Path.Combine(Path.GetTempPath(), $"mcp-shell-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        // Create some test files
        File.WriteAllText(Path.Combine(_testDirectory, "test.json"), "{\"message\": \"Hello World\"}");
        File.WriteAllText(Path.Combine(_testDirectory, "test.txt"), "Test content");
        
        _executionService = new CommandExecutionService(_testDirectory);
        _sut = new ShellTools(_executionService);
    }

    [Fact]
    public async Task ExecuteAsync_WithSimpleEcho_ReturnsSuccessResponse()
    {
        // Arrange
        var command = "echo";
        var arguments = new[] { "Hello from MCP" };

        // Act
        var result = await _sut.ShellExecuteAsync(command, arguments);

        // Assert
        result.ShouldNotBeNull();
        result.ExitCode.ShouldBe(0);
        result.TimedOut.ShouldBeFalse();
        result.Stdout.ShouldContain("Hello from MCP");
        result.ExecutionTimeMs.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ExecuteAsync_WithDotnetVersion_ReturnsVersionInfo()
    {
        // Arrange
        var command = "dotnet";
        var arguments = new[] { "--version" };

        // Act
        var result = await _sut.ShellExecuteAsync(command, arguments);

        // Assert
        result.ShouldNotBeNull();
        result.ExitCode.ShouldBe(0);
        result.TimedOut.ShouldBeFalse();
        result.Stdout.ShouldNotBeEmpty();
        result.Stderr.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WithCustomTimeout_RespectsTimeout()
    {
        // Arrange - sleep command that will timeout
        var command = "sleep";
        var arguments = new[] { "10" };
        var timeout = 1;

        // Act
        var result = await _sut.ShellExecuteAsync(command, arguments, timeoutSeconds: timeout);

        // Assert
        result.ShouldNotBeNull();
        result.TimedOut.ShouldBeTrue();
        result.ExitCode.ShouldBe(-1);
    }

    [Fact]
    public async Task ExecuteAsync_WithWorkingDirectory_ExecutesInCorrectDirectory()
    {
        // Arrange
        var command = "pwd";

        // Act
        var result = await _sut.ShellExecuteAsync(command, workingDirectory: _testDirectory);

        // Assert
        result.ShouldNotBeNull();
        result.ExitCode.ShouldBe(0);
        result.Stdout.Trim().ShouldBe(_testDirectory);
    }

    [Fact]
    public async Task ExecuteJsonAsync_WithValidJson_ParsesSuccessfully()
    {
        // Arrange
        var command = "cat";
        var arguments = new[] { "test.json" };

        // Act
        var result = await _sut.ShellExecuteJsonAsync(command, arguments, workingDirectory: _testDirectory);

        // Assert
        result.ShouldNotBeNull();
        result.ExitCode.ShouldBe(0);
        result.TimedOut.ShouldBeFalse();
        result.Json.ShouldNotBeNull();
        result.ParseError.ShouldBeNull();
        result.Json!["message"]!.ToString().ShouldBe("Hello World");
    }

    [Fact]
    public async Task ExecuteJsonAsync_WithJqTool_ParsesAndFiltersJson()
    {
        // Arrange - use jq to extract a field if available
        var command = "jq";
        var arguments = new[] { ".message", "test.json" };

        // Act
        var result = await _sut.ShellExecuteJsonAsync(command, arguments, workingDirectory: _testDirectory);

        // Assert - jq might not be installed locally, so check if it exists first
        if (result.ExitCode == 0)
        {
            result.Json.ShouldNotBeNull();
            result.ParseError.ShouldBeNull();
            result.Json!.ToString().ShouldContain("Hello World");
        }
        else
        {
            // Expected if jq is not installed on the test machine
            result.Stderr.ShouldContain("Execution failed");
        }
    }

    [Fact]
    public async Task ExecuteJsonAsync_WithInvalidJson_ReturnsParseError()
    {
        // Arrange - echo invalid JSON
        var command = "echo";
        var arguments = new[] { "not valid json" };

        // Act
        var result = await _sut.ShellExecuteJsonAsync(command, arguments);

        // Assert
        result.ShouldNotBeNull();
        result.ExitCode.ShouldBe(0);
        result.Json.ShouldBeNull();
        result.ParseError.ShouldNotBeNull();
        result.ParseError.ShouldContain("Failed to parse JSON");
    }

    [Fact]
    public async Task ExecuteJsonAsync_WithCommandError_ReturnsErrorResult()
    {
        // Arrange - command that will fail
        var command = "cat";
        var arguments = new[] { "nonexistent.json" };

        // Act
        var result = await _sut.ShellExecuteJsonAsync(command, arguments, workingDirectory: _testDirectory);

        // Assert
        result.ShouldNotBeNull();
        result.ExitCode.ShouldNotBe(0);
        result.Json.ShouldBeNull();
    }

    [Fact]
    public async Task GetAvailableToolsAsync_ReturnsToolList()
    {
        // Act
        var result = await _sut.ShellGetAvailableToolsAsync();

        // Assert
        result.ShouldNotBeNull();
        result.Tools.ShouldNotBeEmpty();
        result.WorkspacePath.ShouldNotBeNullOrEmpty();
        
        // Check for expected tools
        var dotnetTool = result.Tools.FirstOrDefault(t => t.Name == "dotnet");
        dotnetTool.ShouldNotBeNull();
        dotnetTool.Available.ShouldBeTrue(); // dotnet should always be available in test environment
        dotnetTool.Version.ShouldNotBeNullOrEmpty();
        
        var bashTool = result.Tools.FirstOrDefault(t => t.Name == "bash");
        bashTool.ShouldNotBeNull();
        bashTool.Available.ShouldBeTrue(); // bash should always be available on Linux
        
        var gitTool = result.Tools.FirstOrDefault(t => t.Name == "git");
        gitTool.ShouldNotBeNull();
        gitTool.Available.ShouldBeTrue(); // git should always be available
    }

    [Fact]
    public async Task GetAvailableToolsAsync_MarksUnavailableToolsCorrectly()
    {
        // Act
        var result = await _sut.ShellGetAvailableToolsAsync();

        // Assert
        result.ShouldNotBeNull();
        
        // Some tools might not be available on the test machine
        // Check that tools are marked correctly based on their actual availability
        foreach (var tool in result.Tools)
        {
            if (tool.Available)
            {
                tool.Version.ShouldNotBeNullOrEmpty();
            }
            else
            {
                tool.Version.ShouldBeNull();
            }
        }
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
