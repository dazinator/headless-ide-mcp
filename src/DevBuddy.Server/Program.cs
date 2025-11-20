using DevBuddy.Core;
using DevBuddy.Core.ProcessExecution;
using DevBuddy.Server;
using DevBuddy.Server.Components;
using DevBuddy.Server.Data;
using DevBuddy.Server.Jobs;
using DevBuddy.Server.Services;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to listen on both HTTP and HTTPS ports
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(8080); // HTTP
    serverOptions.ListenAnyIP(8081, listenOptions =>
    {
        listenOptions.UseHttps(); // HTTPS - certificate configured via environment variables
    });
});

// Get the code base path from environment variable or use default
var codeBasePath = Environment.GetEnvironmentVariable("CODE_BASE_PATH") ?? "/workspace";

// Get the database path from environment variable or use default
var dbPath = Environment.GetEnvironmentVariable("DB_PATH");
if (string.IsNullOrEmpty(dbPath))
{
    // Check if /data directory exists and is writable, otherwise use temp
    if (Directory.Exists("/data") && IsDirectoryWritable("/data"))
    {
        dbPath = "/data/devbuddy.db";
    }
    else
    {
        dbPath = Path.Combine(Path.GetTempPath(), "devbuddy.db");
    }
}

// Ensure the database directory exists
var dbDirectory = Path.GetDirectoryName(dbPath);
if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
{
    Directory.CreateDirectory(dbDirectory);
}

// Load command execution options from configuration
var commandExecutionOptions = new CommandExecutionOptions();
builder.Configuration.GetSection("CommandExecution").Bind(commandExecutionOptions);

// Register services
builder.Services.AddSingleton<IFileSystemService>(sp => new FileSystemService(codeBasePath));
builder.Services.AddSingleton<ICommandExecutionService>(sp => 
{
    var logger = sp.GetRequiredService<ILogger<CommandExecutionService>>();
    return new CommandExecutionService(codeBasePath, commandExecutionOptions, logger);
});

// Add DbContext with SQLite
builder.Services.AddDbContext<DevBuddyDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// Add Git Repository Service
builder.Services.AddScoped<IGitRepositoryService, GitRepositoryService>();

// Add Blazor components
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add background services for git repository management
builder.Services.AddHostedService<AutoCloneBackgroundService>();
builder.Services.AddHostedService<AutoFetchBackgroundService>();

// Configure MCP Server
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<DevBuddyDbContext>();
    dbContext.Database.EnsureCreated();
}

// Add API key authentication middleware (must be before MapMcp)
app.UseApiKeyAuthentication();

// Configure static files for Blazor
app.UseStaticFiles();
app.UseAntiforgery();

// Map Blazor components first
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Map MCP endpoints
app.MapMcp();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", codeBasePath }));

app.Run();

// Helper method to check if directory is writable
static bool IsDirectoryWritable(string dirPath)
{
    try
    {
        var testFile = Path.Combine(dirPath, Path.GetRandomFileName());
        using (File.Create(testFile)) { }
        File.Delete(testFile);
        return true;
    }
    catch
    {
        return false;
    }
}

// Make Program class accessible to tests
public partial class Program { }


