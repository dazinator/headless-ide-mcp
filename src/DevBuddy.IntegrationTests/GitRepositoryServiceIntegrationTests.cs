using DevBuddy.Server.Data;
using DevBuddy.Server.Data.Models;
using DevBuddy.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shouldly;
using System.Diagnostics;

namespace DevBuddy.IntegrationTests;

/// <summary>
/// Integration tests for the GitRepositoryService
/// These tests verify branch checkout functionality with real git operations
/// </summary>
public class GitRepositoryServiceIntegrationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _testRepoPath;
    private readonly DevBuddyDbContext _dbContext;
    private readonly GitRepositoryService _sut;

    public GitRepositoryServiceIntegrationTests()
    {
        // Create a temporary test directory
        _testDirectory = Path.Combine(Path.GetTempPath(), $"git-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _testRepoPath = Path.Combine(_testDirectory, "test-repo");

        // Setup in-memory database
        var options = new DbContextOptionsBuilder<DevBuddyDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new DevBuddyDbContext(options);

        // Setup configuration
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "GitRepositoriesPath", _testDirectory }
            })
            .Build();

        // Create logger
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<GitRepositoryService>();

        _sut = new GitRepositoryService(_dbContext, logger, config);

        // Initialize a test git repository with multiple branches
        InitializeTestRepository();
    }

    private void InitializeTestRepository()
    {
        Directory.CreateDirectory(_testRepoPath);
        
        // Initialize git repo
        ExecuteGit(_testRepoPath, "init");
        ExecuteGit(_testRepoPath, "config user.email \"test@test.com\"");
        ExecuteGit(_testRepoPath, "config user.name \"Test User\"");
        
        // Create initial commit on master (default branch)
        File.WriteAllText(Path.Combine(_testRepoPath, "README.md"), "# Test Repo");
        ExecuteGit(_testRepoPath, "add .");
        ExecuteGit(_testRepoPath, "commit -m \"Initial commit\"");
        
        // Rename to main for consistency
        try
        {
            ExecuteGit(_testRepoPath, "branch -m master main");
        }
        catch
        {
            // If already on main, ignore
        }
        
        // Create develop branch
        ExecuteGit(_testRepoPath, "checkout -b develop");
        File.WriteAllText(Path.Combine(_testRepoPath, "dev.txt"), "Dev content");
        ExecuteGit(_testRepoPath, "add .");
        ExecuteGit(_testRepoPath, "commit -m \"Dev commit\"");
        
        // Create feature branch
        ExecuteGit(_testRepoPath, "checkout -b feature/test");
        File.WriteAllText(Path.Combine(_testRepoPath, "feature.txt"), "Feature content");
        ExecuteGit(_testRepoPath, "add .");
        ExecuteGit(_testRepoPath, "commit -m \"Feature commit\"");
        
        // Go back to main
        ExecuteGit(_testRepoPath, "checkout main");
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

    [Fact]
    public async Task CheckoutBranchAsync_WithLocalBranch_SwitchesSuccessfully()
    {
        // Arrange
        var repo = new GitRepositoryConfiguration
        {
            Name = "test-repo",
            RemoteUrl = "https://github.com/test/repo.git",
            LocalPath = "test-repo",
            CloneStatus = CloneStatus.Cloned
        };
        _dbContext.GitRepositories.Add(repo);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.CheckoutBranchAsync(repo.Id, "develop");

        // Assert
        result.ShouldBeTrue();
        var currentBranch = ExecuteGit(_testRepoPath, "rev-parse --abbrev-ref HEAD").Trim();
        currentBranch.ShouldBe("develop");
    }

    [Fact]
    public async Task CheckoutBranchAsync_WithRemoteBranchFormat_CreatesLocalTrackingBranch()
    {
        // Arrange
        var repo = new GitRepositoryConfiguration
        {
            Name = "test-repo",
            RemoteUrl = "https://github.com/test/repo.git",
            LocalPath = "test-repo",
            CloneStatus = CloneStatus.Cloned
        };
        _dbContext.GitRepositories.Add(repo);
        await _dbContext.SaveChangesAsync();

        // Setup: Create a "remote" by adding origin
        ExecuteGit(_testRepoPath, "remote add origin https://github.com/test/repo.git");
        
        // Delete local develop branch to simulate fresh clone scenario
        ExecuteGit(_testRepoPath, "checkout main");
        ExecuteGit(_testRepoPath, "branch -D develop");
        
        // Create a remote tracking branch without local branch
        ExecuteGit(_testRepoPath, "branch -r");
        
        // Manually create the remote ref to simulate a fetched remote branch
        var remoteBranchRef = Path.Combine(_testRepoPath, ".git", "refs", "remotes", "origin", "develop");
        Directory.CreateDirectory(Path.GetDirectoryName(remoteBranchRef)!);
        
        // Get the commit SHA for develop from the deleted branch
        var developSha = ExecuteGit(_testRepoPath, "rev-parse main").Trim(); // Use main's SHA as placeholder
        File.WriteAllText(remoteBranchRef, developSha);

        // Act - Create new branch tracking remote using explicit API
        var result = await _sut.CheckoutBranchAsync(repo.Id, "develop", createNew: true, trackRemoteBranch: "origin/develop");

        // Assert
        result.ShouldBeTrue();
        var currentBranch = ExecuteGit(_testRepoPath, "rev-parse --abbrev-ref HEAD").Trim();
        currentBranch.ShouldBe("develop");
        
        // Verify local branch was created
        var branches = ExecuteGit(_testRepoPath, "branch --list develop");
        branches.ShouldContain("develop");
    }

    [Fact]
    public async Task CheckoutBranchAsync_WithCreateNew_CreatesNewBranch()
    {
        // Arrange
        var repo = new GitRepositoryConfiguration
        {
            Name = "test-repo",
            RemoteUrl = "https://github.com/test/repo.git",
            LocalPath = "test-repo",
            CloneStatus = CloneStatus.Cloned
        };
        _dbContext.GitRepositories.Add(repo);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.CheckoutBranchAsync(repo.Id, "new-branch", createNew: true);

        // Assert
        result.ShouldBeTrue();
        var currentBranch = ExecuteGit(_testRepoPath, "rev-parse --abbrev-ref HEAD").Trim();
        currentBranch.ShouldBe("new-branch");
    }

    [Fact]
    public async Task CheckoutBranchAsync_WithExistingLocalBranchMatchingRemote_ChecksOutLocal()
    {
        // Arrange
        var repo = new GitRepositoryConfiguration
        {
            Name = "test-repo",
            RemoteUrl = "https://github.com/test/repo.git",
            LocalPath = "test-repo",
            CloneStatus = CloneStatus.Cloned
        };
        _dbContext.GitRepositories.Add(repo);
        await _dbContext.SaveChangesAsync();

        // Setup: Ensure we're on main and develop exists locally
        ExecuteGit(_testRepoPath, "checkout main");

        // Act - Checkout existing local branch
        var result = await _sut.CheckoutBranchAsync(repo.Id, "develop", createNew: false);

        // Assert
        result.ShouldBeTrue();
        var currentBranch = ExecuteGit(_testRepoPath, "rev-parse --abbrev-ref HEAD").Trim();
        currentBranch.ShouldBe("develop");
    }

    [Fact]
    public async Task GetLocalAndRemoteBranchesAsync_ReturnsCorrectBranches()
    {
        // Arrange
        var repo = new GitRepositoryConfiguration
        {
            Name = "test-repo",
            RemoteUrl = "https://github.com/test/repo.git",
            LocalPath = "test-repo",
            CloneStatus = CloneStatus.Cloned
        };
        _dbContext.GitRepositories.Add(repo);
        await _dbContext.SaveChangesAsync();

        // Setup remote
        ExecuteGit(_testRepoPath, "remote add origin https://github.com/test/repo.git");
        
        // Create remote refs to simulate fetched branches
        var remoteRefsDir = Path.Combine(_testRepoPath, ".git", "refs", "remotes", "origin");
        Directory.CreateDirectory(remoteRefsDir);
        
        var mainSha = ExecuteGit(_testRepoPath, "rev-parse main").Trim();
        File.WriteAllText(Path.Combine(remoteRefsDir, "main"), mainSha);
        
        var developSha = ExecuteGit(_testRepoPath, "rev-parse develop").Trim();
        File.WriteAllText(Path.Combine(remoteRefsDir, "develop"), developSha);

        // Act
        var (localBranches, remoteBranches) = await _sut.GetLocalAndRemoteBranchesAsync(repo.Id);

        // Assert
        localBranches.ShouldContain("main");
        localBranches.ShouldContain("develop");
        localBranches.ShouldContain("feature/test");
        
        remoteBranches.ShouldContain("origin/main");
        remoteBranches.ShouldContain("origin/develop");
    }

    public void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
        _dbContext.Dispose();
    }
}
