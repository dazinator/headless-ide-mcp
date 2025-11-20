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
    Task<bool> CheckoutBranchAsync(int id, string branchName, bool createNew = false);
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
                // Check if remote URL matches
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
                    
                    // Get current branch
                    config.CurrentBranch = await ExecuteGitCommandAsync(fullPath, "rev-parse --abbrev-ref HEAD");
                    
                    // Check for uncommitted changes
                    var statusOutput = await ExecuteGitCommandAsync(fullPath, "status --porcelain");
                    config.HasUncommittedChanges = !string.IsNullOrWhiteSpace(statusOutput);
                    
                    // Get commits ahead/behind
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
                        // Upstream might not be set
                        config.CommitsAhead = 0;
                        config.CommitsBehind = 0;
                    }
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

    public async Task<bool> CheckoutBranchAsync(int id, string branchName, bool createNew = false)
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
                await ExecuteGitCommandAsync(fullPath, $"checkout -b {branchName}");
            }
            else
            {
                // Check if this is a remote branch reference (e.g., "remotes/origin/develop" or "origin/develop")
                var isRemoteBranch = branchName.StartsWith("remotes/") || 
                                     (branchName.StartsWith("origin/") && !branchName.Contains("HEAD"));
                
                if (isRemoteBranch)
                {
                    // Extract the local branch name from the remote reference
                    string localBranchName;
                    if (branchName.StartsWith("remotes/origin/"))
                    {
                        localBranchName = branchName.Substring("remotes/origin/".Length);
                    }
                    else if (branchName.StartsWith("origin/"))
                    {
                        localBranchName = branchName.Substring("origin/".Length);
                    }
                    else
                    {
                        // For other remotes like "remotes/upstream/branch"
                        var parts = branchName.Split('/');
                        localBranchName = parts.Length > 2 ? parts[^1] : branchName;
                    }
                    
                    // Check if a local branch with this name already exists
                    var localBranchesOutput = await ExecuteGitCommandAsync(fullPath, "branch --list");
                    var localBranches = localBranchesOutput
                        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                        .Select(b => b.Trim().TrimStart('*').Trim())
                        .ToList();
                    
                    if (localBranches.Contains(localBranchName))
                    {
                        // Local branch exists, just check it out
                        await ExecuteGitCommandAsync(fullPath, $"checkout {localBranchName}");
                    }
                    else
                    {
                        // Create a new local tracking branch
                        await ExecuteGitCommandAsync(fullPath, $"checkout -b {localBranchName} --track {branchName}");
                    }
                    
                    branchName = localBranchName;
                }
                else
                {
                    // Regular local branch checkout
                    await ExecuteGitCommandAsync(fullPath, $"checkout {branchName}");
                }
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
}
