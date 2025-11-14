# Project Setup

This document describes the architecture and setup of the Headless IDE MCP server project.

## Architecture

The project follows a clean architecture approach with separation of concerns:

### Projects

#### HeadlessIdeMcp.Server
- **Type**: ASP.NET Core Web Application (Minimal API)
- **Purpose**: Hosts the MCP server and exposes tools via HTTP
- **Dependencies**: 
  - `ModelContextProtocol.AspNetCore` - Official MCP SDK for ASP.NET Core
  - `HeadlessIdeMcp.Core` - Core business logic

**Key Files:**
- `Program.cs` - Application entry point, configures MCP server and dependency injection
- `FileSystemTools.cs` - MCP tool implementations decorated with `[McpServerTool]` attributes

#### HeadlessIdeMcp.Core
- **Type**: Class Library
- **Purpose**: Contains core business logic for tools, independent of MCP/HTTP concerns
- **Dependencies**: None (pure .NET 8.0)

**Key Files:**
- `IFileSystemService.cs` - Interface for file system operations
- `FileSystemService.cs` - Implementation of file system operations with configurable base path

#### HeadlessIdeMcp.IntegrationTests
- **Type**: xUnit Test Project
- **Purpose**: Integration tests that verify tools against real file system
- **Dependencies**: 
  - `xunit` - Testing framework
  - `Shouldly` - Assertion library
  - `HeadlessIdeMcp.Core` - System under test

**Key Files:**
- `FileSystemServiceIntegrationTests.cs` - Integration tests with no mocked dependencies

## MCP Integration

### Model Context Protocol (MCP)

MCP is a protocol that allows AI assistants and other clients to discover and invoke tools/capabilities exposed by servers. This project uses the official [C# MCP SDK](https://github.com/modelcontextprotocol/csharp-sdk) maintained by Microsoft and Anthropic.

### How Tools are Registered

1. Tools are defined as classes decorated with `[McpServerToolType]` attribute
2. Methods decorated with `[McpServerTool]` attribute become callable tools
3. The SDK automatically discovers these tools via `WithToolsFromAssembly()`
4. Dependency injection is supported - constructor parameters are resolved from the DI container

### Example Tool Definition

```csharp
[McpServerToolType]
public class FileSystemTools
{
    private readonly IFileSystemService _fileSystemService;

    public FileSystemTools(IFileSystemService fileSystemService)
    {
        _fileSystemService = fileSystemService;
    }

    [McpServerTool]
    [Description("Checks if a specific file exists in the code base")]
    public string CheckFileExists(
        [Description("The file path to check")] 
        string fileName)
    {
        bool exists = _fileSystemService.FileExists(fileName);
        return exists 
            ? $"File '{fileName}' exists" 
            : $"File '{fileName}' does not exist";
    }
}
```

### MCP JSON-RPC Protocol

The server exposes endpoints at the root path `/` that accept JSON-RPC 2.0 requests:

**List Tools:**
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/list"
}
```

**Call Tool:**
```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "tools/call",
  "params": {
    "name": "check_file_exists",
    "arguments": {
      "fileName": "path/to/file.cs"
    }
  }
}
```

## Docker Setup

### Dockerfile

The Dockerfile uses a multi-stage build:
1. **Build stage**: Uses SDK image to restore and build the application
2. **Publish stage**: Publishes the application in Release mode
3. **Runtime stage**: Uses minimal ASP.NET runtime image with published app

### Docker Compose

The `docker-compose.yml` configures:
- Container name and ports mapping (5000:8080)
- Volume mount: `./sample-codebase:/workspace:ro` (read-only)
- Environment variables for ASP.NET Core and code base path
- Custom network for potential multi-container scenarios

### Visual Studio Docker Compose Project (.dcproj)

The `.dcproj` file enables:
- F5 debugging directly from Visual Studio 2022
- Automatic Docker image build and container start
- Browser launch to the `/health` endpoint
- Full debugging support with breakpoints

## Testing Strategy

### Integration Testing Approach

The project uses **integration tests** rather than unit tests for tool validation:

**Benefits:**
- Tests run against real file system, not mocks
- Validates actual behavior end-to-end
- Ensures tools work correctly in production-like conditions
- Simpler test setup - no complex mocking infrastructure needed

**Test Structure:**
```csharp
public class FileSystemServiceIntegrationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly FileSystemService _sut;

    public FileSystemServiceIntegrationTests()
    {
        // Create real test files on disk
        _testDirectory = Path.Combine(Path.GetTempPath(), $"mcp-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        File.WriteAllText(Path.Combine(_testDirectory, "TestFile.cs"), "...");
        
        _sut = new FileSystemService(_testDirectory);
    }

    [Fact]
    public void FileExists_WithExistingFile_ReturnsTrue()
    {
        // Test against real file system
        var result = _sut.FileExists("TestFile.cs");
        result.ShouldBeTrue();
    }

    public void Dispose()
    {
        // Clean up real test files
        Directory.Delete(_testDirectory, recursive: true);
    }
}
```

### Sample Codebase for Testing

The `sample-codebase/` directory contains a real .NET solution used for:
- Integration test validation
- Docker volume mount demonstration
- Runtime tool testing via `.http` file

This approach ensures tools are tested against realistic .NET project structures.

## Adding New Tools

To add a new MCP tool:

1. **Add business logic to Core project:**
   ```csharp
   // HeadlessIdeMcp.Core/IMyNewService.cs
   public interface IMyNewService
   {
       string DoSomething(string input);
   }
   
   // HeadlessIdeMcp.Core/MyNewService.cs
   public class MyNewService : IMyNewService
   {
       public string DoSomething(string input) { /* logic */ }
   }
   ```

2. **Register service in Program.cs:**
   ```csharp
   builder.Services.AddSingleton<IMyNewService, MyNewService>();
   ```

3. **Create MCP tool in Server project:**
   ```csharp
   // HeadlessIdeMcp.Server/MyNewTools.cs
   [McpServerToolType]
   public class MyNewTools
   {
       private readonly IMyNewService _service;
       
       public MyNewTools(IMyNewService service)
       {
           _service = service;
       }
       
       [McpServerTool]
       [Description("Does something useful")]
       public string DoSomethingTool(
           [Description("Input parameter")] string input)
       {
           return _service.DoSomething(input);
       }
   }
   ```

4. **Add integration tests:**
   ```csharp
   // HeadlessIdeMcp.IntegrationTests/MyNewServiceIntegrationTests.cs
   public class MyNewServiceIntegrationTests
   {
       [Fact]
       public void DoSomething_WithValidInput_ReturnsExpectedResult()
       {
           var sut = new MyNewService();
           var result = sut.DoSomething("test");
           result.ShouldNotBeNullOrEmpty();
       }
   }
   ```

5. **Add HTTP request examples to test-mcp-server.http:**
   ```http
   ### Call DoSomethingTool
   POST http://localhost:5000/
   Content-Type: application/json
   
   {
     "jsonrpc": "2.0",
     "id": 5,
     "method": "tools/call",
     "params": {
       "name": "do_something_tool",
       "arguments": {
         "input": "test value"
       }
     }
   }
   ```

## Development Workflow

1. **Make code changes** in Core or Server projects
2. **Build**: `dotnet build` from `src/` directory
3. **Run tests**: `dotnet test` to ensure integration tests pass
4. **Test locally**: 
   - Run with `dotnet run` from Server directory, OR
   - Run with `docker-compose up --build`
5. **Verify with .http file**: Send test requests to the root endpoint `/`
6. **Debug in VS2022**: Set docker-compose as startup project and F5

## CI/CD Considerations

The project is ready for CI/CD pipelines:
- All tests run via `dotnet test` with no external dependencies
- Docker image builds with `docker build`
- Integration tests verify end-to-end functionality
- Sample codebase is committed to repository for consistent testing

## Package Management

The solution uses **Central Package Management**:
- Package versions are defined in `src/Directory.Packages.props`
- Project files reference packages without versions
- Ensures consistent versions across all projects
- Simplifies dependency updates

## Environment Configuration

Key configuration points:

1. **CODE_BASE_PATH**: Where to find the code to analyze
   - Local development: Set via environment variable or use default
   - Docker: Set to `/workspace` and mount volume
   - Integration tests: Points to `sample-codebase/` directory

2. **ASP.NET Core ports**:
   - Development: 5000 (HTTP)
   - Docker internal: 8080 (HTTP)
   - Configurable via environment variables

## Security Considerations

- Sample codebase mounted **read-only** in Docker (`ro` flag)
- No authentication implemented yet (add as needed)
- File system access is restricted to configured base path
- No file write operations in current tool set
