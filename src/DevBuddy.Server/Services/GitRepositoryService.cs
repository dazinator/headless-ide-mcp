using DevBuddy.Server.Data;
using DevBuddy.Server.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace DevBuddy.Server.Services;

public interface IGitRepositoryService
{
    Task<List<GitRepositoryConfiguration>> GetAllAsync();
    Task<GitRepositoryConfiguration?> GetByIdAsync(int id);
    Task<GitRepositoryConfiguration> CreateAsync(GitRepositoryConfiguration config);
    Task UpdateAsync(GitRepositoryConfiguration config);
    Task DeleteAsync(int id);
    Task<bool> CheckRepositoryStatusAsync(int id);
    Task<List<string>> GetBranchesAsync(int id);
    Task<(List<string> LocalBranches, List<string> RemoteBranches)> GetLocalAndRemoteBranchesAsync(int id);
    Task<bool> CheckoutBranchAsync(int id, string branchName, bool createNew = false, string? trackRemoteBranch = null);
    Task<GitRepositoryConfiguration> ImportExistingRepositoryAsync(string relativePath);
}

public class GitRepositoryService : IGitRepositoryService
{
    private readonly DevBuddyDbContext _context;
    private readonly ILogger<GitRepositoryService> _logger;
    private readonly string _gitReposBasePath;

    public GitRepositoryService(
        DevBuddyDbContext context,
        ILogger<GitRepositoryService> logger,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _gitReposBasePath = configuration["GitRepositoriesPath"] ?? "/git-repos";
    }

    public async Task<List<GitRepositoryConfiguration>> GetAllAsync()
    {
        return await _context.GitRepositories.ToListAsync();
    }

    public async Task<GitRepositoryConfiguration?> GetByIdAsync(int id)
    {
        return await _context.GitRepositories.FindAsync(id);
    }

    public async Task<GitRepositoryConfiguration> CreateAsync(GitRepositoryConfiguration config)
    {
        config.CreatedAt = DateTime.UtcNow;
        config.UpdatedAt = DateTime.UtcNow;
        config.CloneStatus = CloneStatus.NotCloned;
        
        _context.GitRepositories.Add(config);
        await _context.SaveChangesAsync();
        
        // If this is a local repo (no remote URL), initialize it immediately
        if (string.IsNullOrWhiteSpace(config.RemoteUrl))
        {
            await InitializeLocalRepositoryAsync(config);
        }
        
        return config;
    }

    public async Task UpdateAsync(GitRepositoryConfiguration config)
    {
        config.UpdatedAt = DateTime.UtcNow;
        _context.GitRepositories.Update(config);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var config = await _context.GitRepositories.FindAsync(id);
        if (config != null)
        {
            _context.GitRepositories.Remove(config);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> CheckRepositoryStatusAsync(int id)
    {
        var config = await GetByIdAsync(id);
        if (config == null) return false;

        try
        {
            var fullPath = Path.Combine(_gitReposBasePath, config.LocalPath);
            
            if (!Directory.Exists(fullPath))
            {
                config.CloneStatus = CloneStatus.NotCloned;
                config.CurrentBranch = null;
                config.CommitsAhead = 0;
                config.CommitsBehind = 0;
                config.HasUncommittedChanges = false;
            }
            else if (!Directory.Exists(Path.Combine(fullPath, ".git")))
            {
                config.CloneStatus = CloneStatus.Conflict;
                config.ErrorMessage = "Directory exists but is not a git repository";
            }
            else
            {
                // Check if remote URL matches (only for repos with remotes)
                if (!string.IsNullOrWhiteSpace(config.RemoteUrl))
                {
                    var remoteUrl = await ExecuteGitCommandAsync(fullPath, "config --get remote.origin.url");
                    if (!string.IsNullOrEmpty(remoteUrl) && remoteUrl.Trim() != config.RemoteUrl.Trim())
                    {
                        config.CloneStatus = CloneStatus.Conflict;
                        config.ErrorMessage = $"Repository exists but has different remote URL: {remoteUrl}";
                    }
                    else
                    {
                        config.CloneStatus = CloneStatus.Cloned;
                        config.ErrorMessage = null;
                        await UpdateRepositoryStatusDetails(config, fullPath);
                    }
                }
                else
                {
                    // Local repo without remote
                    config.CloneStatus = CloneStatus.Cloned;
                    config.ErrorMessage = null;
                    await UpdateRepositoryStatusDetails(config, fullPath);
                }
            }

            config.LastChecked = DateTime.UtcNow;
            await UpdateAsync(config);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking repository status for {RepoId}", id);
            config.CloneStatus = CloneStatus.Failed;
            config.ErrorMessage = ex.Message;
            await UpdateAsync(config);
            return false;
        }
    }

    private async Task UpdateRepositoryStatusDetails(GitRepositoryConfiguration config, string fullPath)
    {
        // Get current branch (handle fresh repos with no commits)
        try
        {
            config.CurrentBranch = await ExecuteGitCommandAsync(fullPath, "rev-parse --abbrev-ref HEAD");
        }
        catch
        {
            // Fresh repository with no commits - try to get default branch name
            try
            {
                config.CurrentBranch = await ExecuteGitCommandAsync(fullPath, "symbolic-ref --short HEAD");
            }
            catch
            {
                config.CurrentBranch = null;
            }
        }
        
        // Check for uncommitted changes
        var statusOutput = await ExecuteGitCommandAsync(fullPath, "status --porcelain");
        config.HasUncommittedChanges = !string.IsNullOrWhiteSpace(statusOutput);
        
        // Get commits ahead/behind (only if remote is configured)
        try
        {
            var aheadBehind = await ExecuteGitCommandAsync(fullPath, "rev-list --left-right --count HEAD...@{u}");
            if (!string.IsNullOrWhiteSpace(aheadBehind))
            {
                var parts = aheadBehind.Trim().Split('\t');
                if (parts.Length == 2)
                {
                    config.CommitsAhead = int.TryParse(parts[0], out var ahead) ? ahead : 0;
                    config.CommitsBehind = int.TryParse(parts[1], out var behind) ? behind : 0;
                }
            }
        }
        catch
        {
            // Upstream might not be set (expected for local repos)
            config.CommitsAhead = 0;
            config.CommitsBehind = 0;
        }
    }

    private async Task InitializeLocalRepositoryAsync(GitRepositoryConfiguration config)
    {
        try
        {
            _logger.LogInformation("Initializing local repository {RepoName}", config.Name);
            
            var fullPath = Path.Combine(_gitReposBasePath, config.LocalPath);
            var parentDir = Path.GetDirectoryName(fullPath);
            
            if (!string.IsNullOrEmpty(parentDir))
            {
                Directory.CreateDirectory(parentDir);
            }

            // Create the directory if it doesn't exist
            Directory.CreateDirectory(fullPath);

            // Initialize git repository
            await ExecuteGitCommandAsync(fullPath, "init");
            
            config.CloneStatus = CloneStatus.Cloned;
            config.ErrorMessage = null;
            config.LastChecked = DateTime.UtcNow;
            
            await UpdateAsync(config);
            
            _logger.LogInformation("Successfully initialized local repository {RepoName}", config.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize local repository {RepoName}", config.Name);
            config.CloneStatus = CloneStatus.Failed;
            config.ErrorMessage = ex.Message;
            await UpdateAsync(config);
        }
    }

    public async Task<List<string>> GetBranchesAsync(int id)
    {
        var config = await GetByIdAsync(id);
        if (config == null || config.CloneStatus != CloneStatus.Cloned)
        {
            return new List<string>();
        }

        try
        {
            var fullPath = Path.Combine(_gitReposBasePath, config.LocalPath);
            var branchesOutput = await ExecuteGitCommandAsync(fullPath, "branch -a");
            
            var branches = branchesOutput
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(b => b.Trim().TrimStart('*').Trim())
                .Where(b => !string.IsNullOrWhiteSpace(b))
                .ToList();
            
            return branches;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting branches for {RepoId}", id);
            return new List<string>();
        }
    }

    public async Task<(List<string> LocalBranches, List<string> RemoteBranches)> GetLocalAndRemoteBranchesAsync(int id)
    {
        var config = await GetByIdAsync(id);
        if (config == null || config.CloneStatus != CloneStatus.Cloned)
        {
            return (new List<string>(), new List<string>());
        }

        try
        {
            var fullPath = Path.Combine(_gitReposBasePath, config.LocalPath);
            
            // Get local branches
            var localBranchesOutput = await ExecuteGitCommandAsync(fullPath, "branch");
            var localBranches = localBranchesOutput
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(b => b.Trim().TrimStart('*').Trim())
                .Where(b => !string.IsNullOrWhiteSpace(b))
                .ToList();
            
            // Get remote branches
            var remoteBranchesOutput = await ExecuteGitCommandAsync(fullPath, "branch -r");
            var remoteBranches = remoteBranchesOutput
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(b => b.Trim())
                .Where(b => !string.IsNullOrWhiteSpace(b) && !b.Contains("HEAD ->"))
                .ToList();
            
            return (localBranches, remoteBranches);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting branches for {RepoId}", id);
            return (new List<string>(), new List<string>());
        }
    }

    public async Task<bool> CheckoutBranchAsync(int id, string branchName, bool createNew = false, string? trackRemoteBranch = null)
    {
        var config = await GetByIdAsync(id);
        if (config == null || config.CloneStatus != CloneStatus.Cloned)
        {
            return false;
        }

        try
        {
            var fullPath = Path.Combine(_gitReposBasePath, config.LocalPath);
            
            if (createNew)
            {
                // Creating a new branch
                if (!string.IsNullOrEmpty(trackRemoteBranch))
                {
                    // Create new branch tracking a remote branch
                    await ExecuteGitCommandAsync(fullPath, $"checkout -b {branchName} --track {trackRemoteBranch}");
                }
                else
                {
                    // Create new independent branch
                    await ExecuteGitCommandAsync(fullPath, $"checkout -b {branchName}");
                }
            }
            else
            {
                // Checkout existing local branch
                await ExecuteGitCommandAsync(fullPath, $"checkout {branchName}");
            }
            
            config.CurrentBranch = branchName;
            await UpdateAsync(config);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking out branch {BranchName} for {RepoId}", branchName, id);
            return false;
        }
    }

    private async Task<string> ExecuteGitCommandAsync(string workingDirectory, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
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

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        
        await process.WaitForExitAsync();

        if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(error))
        {
            throw new InvalidOperationException($"Git command failed: {error}");
        }

        return output;
    }

    public async Task<GitRepositoryConfiguration> ImportExistingRepositoryAsync(string relativePath)
    {
        var fullPath = Path.Combine(_gitReposBasePath, relativePath);
        
        // Validate that the path exists and is a git repository
        if (!Directory.Exists(fullPath))
        {
            throw new InvalidOperationException($"Directory does not exist: {relativePath}");
        }
        
        var gitDir = Path.Combine(fullPath, ".git");
        if (!Directory.Exists(gitDir))
        {
            throw new InvalidOperationException($"Directory is not a git repository: {relativePath}");
        }
        
        // Check if this path is already imported
        var existingRepo = await _context.GitRepositories
            .FirstOrDefaultAsync(r => r.LocalPath == relativePath);
        
        if (existingRepo != null)
        {
            throw new InvalidOperationException($"Repository is already imported: {relativePath}");
        }
        
        // Extract repository name from the path (last directory name)
        var dirName = Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var baseName = string.IsNullOrEmpty(dirName) ? "imported-repo" : dirName;
        
        // Try to get remote URL
        string? remoteUrl = null;
        try
        {
            remoteUrl = await ExecuteGitCommandAsync(fullPath, "config --get remote.origin.url");
            remoteUrl = remoteUrl?.Trim();
            if (string.IsNullOrWhiteSpace(remoteUrl))
            {
                remoteUrl = null;
            }
        }
        catch
        {
            // No remote configured, which is fine
            remoteUrl = null;
        }
        
        // Create the configuration with retry logic to handle race condition on unique name constraint
        var maxRetries = 3;
        const int maxCounter = 1000; // Safety limit to prevent infinite loop
        
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            // Refresh existing names from database on each attempt to handle race conditions
            var existingNamesList = await _context.GitRepositories
                .Where(r => r.Name == baseName || r.Name.StartsWith(baseName + "-"))
                .Select(r => r.Name)
                .ToListAsync();
            
            // Convert to HashSet for O(1) lookup performance
            var existingNames = new HashSet<string>(existingNamesList);
            
            // Find a unique name
            var repoName = baseName;
            var counter = 1;
            
            while (existingNames.Contains(repoName) && counter < maxCounter)
            {
                repoName = $"{baseName}-{counter}";
                counter++;
            }
            
            if (counter >= maxCounter)
            {
                throw new InvalidOperationException(
                    $"Unable to find unique name for repository '{baseName}' - too many similar names exist (>{maxCounter})");
            }
            
            var config = new GitRepositoryConfiguration
            {
                Name = repoName,
                RemoteUrl = remoteUrl,
                LocalPath = relativePath,
                CloneStatus = CloneStatus.Cloned,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                LastChecked = DateTime.UtcNow
            };
            
            _context.GitRepositories.Add(config);
            
            try
            {
                await _context.SaveChangesAsync();
                
                // Success - update status and return
                await CheckRepositoryStatusAsync(config.Id);
                return await GetByIdAsync(config.Id) ?? config;
            }
            catch (DbUpdateException ex)
            {
                // Handle unique constraint violation due to race condition
                _context.GitRepositories.Remove(config);
                
                if (attempt >= maxRetries)
                {
                    // Final attempt failed, throw the exception
                    throw new InvalidOperationException(
                        $"Failed to import repository after {maxRetries} attempts due to name conflicts. " +
                        $"Last attempted name: {repoName}", ex);
                }
                
                _logger.LogWarning(ex, "Name collision detected for {RepoName}, retrying (attempt {Attempt}/{MaxRetries})", 
                    repoName, attempt, maxRetries);
            }
        }
        
        // Should never reach here - loop always returns on success or throws on final failure
        throw new InvalidOperationException("Unexpected end of import loop");
    }
}
