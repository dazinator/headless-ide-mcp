using DevBuddy.Server.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shouldly;
using System.Diagnostics;

namespace DevBuddy.IntegrationTests;

/// <summary>
/// Integration tests for the FileExplorerService
/// These tests verify git repository detection and file exploration functionality
/// </summary>
public class FileExplorerServiceIntegrationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly FileExplorerService _sut;

    public FileExplorerServiceIntegrationTests()
    {
        // Create a temporary test directory
        _testDirectory = Path.Combine(Path.GetTempPath(), $"file-explorer-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        // Setup configuration
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "GitRepositoriesPath", _testDirectory }
            })
            .Build();

        // Create logger
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<FileExplorerService>();

        _sut = new FileExplorerService(config, logger);
    }

    [Fact]
    public void IsGitRepository_WithGitRepo_ReturnsTrue()
    {
        // Arrange
        var gitRepoPath = Path.Combine(_testDirectory, "git-repo");
        Directory.CreateDirectory(gitRepoPath);
        ExecuteGit(gitRepoPath, "init");

        // Act
        var result = _sut.IsGitRepository("git-repo");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsGitRepository_WithNonGitDirectory_ReturnsFalse()
    {
        // Arrange
        var normalDir = Path.Combine(_testDirectory, "normal-dir");
        Directory.CreateDirectory(normalDir);

        // Act
        var result = _sut.IsGitRepository("normal-dir");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsGitRepository_WithNonExistentDirectory_ReturnsFalse()
    {
        // Act
        var result = _sut.IsGitRepository("non-existent");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task ListDirectoryAsync_WithGitRepo_SetsIsGitRepositoryFlag()
    {
        // Arrange
        var gitRepoPath = Path.Combine(_testDirectory, "test-git-repo");
        Directory.CreateDirectory(gitRepoPath);
        ExecuteGit(gitRepoPath, "init");

        var normalDir = Path.Combine(_testDirectory, "normal-folder");
        Directory.CreateDirectory(normalDir);

        // Act
        var items = await _sut.ListDirectoryAsync("");

        // Assert
        var gitRepoItem = items.FirstOrDefault(i => i.Name == "test-git-repo");
        gitRepoItem.ShouldNotBeNull();
        gitRepoItem.IsGitRepository.ShouldBeTrue();

        var normalDirItem = items.FirstOrDefault(i => i.Name == "normal-folder");
        normalDirItem.ShouldNotBeNull();
        normalDirItem.IsGitRepository.ShouldBeFalse();
    }

    [Fact]
    public async Task ListDirectoryAsync_WithNestedGitRepo_DetectsGitRepo()
    {
        // Arrange
        var parentDir = Path.Combine(_testDirectory, "parent");
        Directory.CreateDirectory(parentDir);
        
        var nestedGitRepo = Path.Combine(parentDir, "nested-git");
        Directory.CreateDirectory(nestedGitRepo);
        ExecuteGit(nestedGitRepo, "init");

        // Act
        var items = await _sut.ListDirectoryAsync("parent");

        // Assert
        var nestedItem = items.FirstOrDefault(i => i.Name == "nested-git");
        nestedItem.ShouldNotBeNull();
        nestedItem.IsGitRepository.ShouldBeTrue();
    }

    [Fact]
    public async Task ListDirectoryAsync_WithFiles_DoesNotMarkAsGitRepository()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.txt");
        File.WriteAllText(testFile, "content");

        // Act
        var items = await _sut.ListDirectoryAsync("");

        // Assert
        var fileItem = items.FirstOrDefault(i => i.Name == "test.txt");
        fileItem.ShouldNotBeNull();
        fileItem.IsGitRepository.ShouldBeFalse();
        fileItem.IsDirectory.ShouldBeFalse();
    }

    private string ExecuteGit(string workingDir, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start git process");
        }

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(error))
        {
            throw new InvalidOperationException($"Git command failed: {error}");
        }

        return output;
    }

    public void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }
}
