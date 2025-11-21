using DevBuddy.Server.Data;
using DevBuddy.Server.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace DevBuddy.Server.Jobs;

public class AutoCloneBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AutoCloneBackgroundService> _logger;
    private readonly IConfiguration _configuration;

    public AutoCloneBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<AutoCloneBackgroundService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AutoClone background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingClones(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AutoClone background service");
            }

            // Wait before checking again (every minute)
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task ProcessPendingClones(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DevBuddyDbContext>();
        var gitReposBasePath = _configuration["GitRepositoriesPath"] ?? "/git-repos";

        // Get repositories that need to be cloned (only those with remote URLs)
        var reposToClone = await dbContext.GitRepositories
            .Where(r => r.CloneStatus == CloneStatus.NotCloned && !string.IsNullOrEmpty(r.RemoteUrl))
            .ToListAsync(stoppingToken);

        foreach (var repo in reposToClone)
        {
            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                // Safety check - should never happen due to LINQ filter above
                if (string.IsNullOrEmpty(repo.RemoteUrl))
                {
                    _logger.LogWarning("Skipping repository {RepoName} - no remote URL configured", repo.Name);
                    continue;
                }

                _logger.LogInformation("Cloning repository {RepoName} from {RemoteUrl}", repo.Name, repo.RemoteUrl);
                
                repo.CloneStatus = CloneStatus.Cloning;
                await dbContext.SaveChangesAsync(stoppingToken);

                var fullPath = Path.Combine(gitReposBasePath, repo.LocalPath);
                var parentDir = Path.GetDirectoryName(fullPath);
                
                if (!string.IsNullOrEmpty(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                }

                await CloneRepositoryAsync(repo.RemoteUrl, fullPath, stoppingToken);

                repo.CloneStatus = CloneStatus.Cloned;
                repo.ErrorMessage = null;
                repo.LastChecked = DateTime.UtcNow;
                
                _logger.LogInformation("Successfully cloned repository {RepoName}", repo.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clone repository {RepoName}", repo.Name);
                repo.CloneStatus = CloneStatus.Failed;
                repo.ErrorMessage = ex.Message;
            }

            await dbContext.SaveChangesAsync(stoppingToken);
        }
    }

    private async Task CloneRepositoryAsync(string remoteUrl, string localPath, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(remoteUrl))
        {
            throw new InvalidOperationException("Remote URL is required for cloning");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"clone {remoteUrl} {localPath}",
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

        await process.WaitForExitAsync(token);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"Git clone failed: {error}");
        }
    }
}
