using HeadlessIdeMcp.Core;
using HeadlessIdeMcp.Core.ProcessExecution;
using HeadlessIdeMcp.Server;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

// Get the code base path from environment variable or use default
var codeBasePath = Environment.GetEnvironmentVariable("CODE_BASE_PATH") ?? "/workspace";

// Register services
builder.Services.AddSingleton<IFileSystemService>(sp => new FileSystemService(codeBasePath));
builder.Services.AddSingleton<ICommandExecutionService>(sp => new CommandExecutionService(codeBasePath));

// Configure MCP Server
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

// Map MCP endpoints
app.MapMcp();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", codeBasePath }));

app.Run();

