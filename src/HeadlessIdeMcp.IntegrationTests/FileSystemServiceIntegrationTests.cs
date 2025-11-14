using HeadlessIdeMcp.Core;
using Shouldly;

namespace HeadlessIdeMcp.IntegrationTests;

/// <summary>
/// Integration tests for the FileSystemService
/// These tests run against the actual file system with no mocked dependencies
/// </summary>
public class FileSystemServiceIntegrationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly FileSystemService _sut;

    public FileSystemServiceIntegrationTests()
    {
        // Create a temporary test directory
        _testDirectory = Path.Combine(Path.GetTempPath(), $"mcp-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        // Create test files
        File.WriteAllText(Path.Combine(_testDirectory, "TestFile.cs"), "// Test file content");
        File.WriteAllText(Path.Combine(_testDirectory, "README.md"), "# Test README");
        
        var subDir = Path.Combine(_testDirectory, "SubDirectory");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "NestedFile.txt"), "Nested content");

        _sut = new FileSystemService(_testDirectory);
    }

    [Fact]
    public void FileExists_WithExistingFile_ReturnsTrue()
    {
        // Arrange
        var fileName = "TestFile.cs";

        // Act
        var result = _sut.FileExists(fileName);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void FileExists_WithNonExistingFile_ReturnsFalse()
    {
        // Arrange
        var fileName = "NonExistent.cs";

        // Act
        var result = _sut.FileExists(fileName);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void FileExists_WithNestedFile_ReturnsTrue()
    {
        // Arrange
        var fileName = Path.Combine("SubDirectory", "NestedFile.txt");

        // Act
        var result = _sut.FileExists(fileName);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void FileExists_WithAbsolutePath_ReturnsTrue()
    {
        // Arrange
        var absolutePath = Path.Combine(_testDirectory, "README.md");

        // Act
        var result = _sut.FileExists(absolutePath);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void FileExists_WithEmptyString_ReturnsFalse()
    {
        // Arrange
        var fileName = "";

        // Act
        var result = _sut.FileExists(fileName);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void FileExists_WithNull_ReturnsFalse()
    {
        // Arrange
        string? fileName = null;

        // Act
        var result = _sut.FileExists(fileName!);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void FileExists_AgainstSampleCodeBase_FindsExpectedFiles()
    {
        // This test demonstrates the integration testing approach
        // where the tool will examine the real sample solution
        
        // Arrange
        var sampleCodeBasePath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "sample-codebase"));
        
        // Only run if sample codebase exists (it should in CI/local builds)
        if (!Directory.Exists(sampleCodeBasePath))
        {
            // Skip test if sample codebase not available
            return;
        }

        var sampleService = new FileSystemService(sampleCodeBasePath);

        // Act & Assert - Check for expected files in sample solution
        sampleService.FileExists("SampleCodeBase.sln").ShouldBeTrue();
        sampleService.FileExists("SampleProject1/Calculator.cs").ShouldBeTrue();
        sampleService.FileExists("SampleProject2/StringHelper.cs").ShouldBeTrue();
        sampleService.FileExists("README.md").ShouldBeTrue();
        
        // Verify non-existent files return false
        sampleService.FileExists("NonExistent.cs").ShouldBeFalse();
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
