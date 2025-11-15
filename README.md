# Headless IDE MCP

A Model Context Protocol (MCP) server built with ASP.NET Core that provides tools for analyzing .NET codebases and executing shell commands in a secure, sandboxed environment. This server exposes MCP tools that can be consumed by AI assistants and other MCP clients to understand and work with .NET projects.

## Features

- **MCP Server**: ASP.NET Core application using the official [ModelContextProtocol.AspNetCore](https://www.nuget.org/packages/ModelContextProtocol.AspNetCore) SDK
- **Shell Command Execution**: Execute CLI commands (dotnet, git, ripgrep, jq, etc.) in a sandboxed environment
- **File System Tools**: Check file existence and analyze project structure
- **Docker Support**: Full containerization with DevContainer base image including development tools
- **VS2022 Debugging**: Docker Compose project for F5 debugging experience
- **Integration Testing**: Real file system and process execution tests with no mocked dependencies
- **Sample Codebase**: Included .NET solution for testing and demonstration
- **Security Controls**: Path validation, timeout enforcement, and command sandboxing

## Quick Start

### Run with Docker Compose

```bash
docker-compose up --build
```

The server will be available at `http://localhost:5000`

### Test the Server

Use the provided `.http/test-mcp-server.http` file with your HTTP client:

```http
### Health Check
GET http://localhost:5000/health

### List Available Tools
POST http://localhost:5000/
Content-Type: application/json

{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/list"
}

### Check File Existence
POST http://localhost:5000/
Content-Type: application/json

{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "tools/call",
  "params": {
    "name": "check_file_exists",
    "arguments": {
      "fileName": "SampleProject1/Calculator.cs"
    }
  }
}
```

## Documentation

- **[Getting Started Guide](docs/getting-started.md)** - Learn how to run and use the MCP server
- **[Project Setup](docs/project-setup.md)** - Understand the architecture and how to add new tools

## Project Structure

```
headless-ide-mcp/
├── src/
│   ├── HeadlessIdeMcp.Server/          # ASP.NET Core MCP server
│   ├── HeadlessIdeMcp.Core/            # Core tool logic
│   ├── HeadlessIdeMcp.IntegrationTests/ # Integration tests
│   └── Solution.sln                     # Main solution
├── sample-codebase/                     # Sample .NET solution for testing
│   ├── SampleProject1/                  # Sample C# project
│   ├── SampleProject2/                  # Sample C# project
│   └── SampleCodeBase.sln
├── docs/                                # Documentation
├── docker-compose.yml                   # Docker Compose configuration
├── docker-compose.dcproj                # VS2022 Docker Compose project
└── Dockerfile                           # Container image definition
```

## Available MCP Tools

### Shell Execution Tools

#### ShellExecuteAsync

Execute a CLI command in a sandboxed environment and get stdout, stderr, and exit code.

**Parameters:**
- `command`: The command to execute (e.g., 'dotnet', 'rg', 'jq')
- `arguments`: Command arguments as array (optional)
- `workingDirectory`: Working directory for command execution (optional, relative to workspace or absolute)
- `timeoutSeconds`: Timeout in seconds (default: 30, max: 300)

**Example:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "ShellExecuteAsync",
    "arguments": {
      "command": "dotnet",
      "arguments": ["--version"]
    }
  }
}
```

#### ShellExecuteJsonAsync

Execute a CLI command that returns JSON output and automatically parse the response.

**Parameters:**
- `command`: The command to execute (e.g., 'dotnet', 'jq')
- `arguments`: Command arguments as array (optional)
- `workingDirectory`: Working directory for command execution (optional)
- `timeoutSeconds`: Timeout in seconds (default: 30, max: 300)

**Example:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "ShellExecuteJsonAsync",
    "arguments": {
      "command": "jq",
      "arguments": [".version", "package.json"]
    }
  }
}
```

#### ShellGetAvailableToolsAsync

Get a list of available CLI tools in the container environment.

**Example:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "ShellGetAvailableToolsAsync",
    "arguments": {}
  }
}
```

**Returns:** List of tools with availability status and versions (dotnet, git, rg, jq, tree, bash, curl, find)

### File System Tools

#### CheckFileExists

Checks if a specific file exists in the code base.

**Parameters:**
- `fileName`: The file path to check (relative or absolute)

**Example:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "check_file_exists",
    "arguments": {
      "fileName": "SampleProject1/Calculator.cs"
    }
  }
}
```

## Development

### Prerequisites

- .NET 8.0 SDK
- Docker Desktop (optional)
- Visual Studio 2022 (optional)

### Build and Test

```bash
cd src
dotnet build
dotnet test
```

### Run Locally

```bash
cd src/HeadlessIdeMcp.Server
export CODE_BASE_PATH=/path/to/your/codebase
dotnet run
```

### Debug with Visual Studio 2022

1. Open the solution in Visual Studio 2022
2. Set `docker-compose` as the startup project
3. Press F5 to start debugging

The sample codebase will be automatically mounted into the container, and you can set breakpoints in the tool implementations.

## Architecture

The project follows clean architecture principles:

- **HeadlessIdeMcp.Server**: HTTP/MCP layer, hosts the ASP.NET Core application
- **HeadlessIdeMcp.Core**: Business logic layer, contains tool implementations independent of MCP
- **HeadlessIdeMcp.IntegrationTests**: Integration tests that verify tools against real file system

MCP tools are discovered automatically through the `[McpServerToolType]` and `[McpServerTool]` attributes, with full dependency injection support.

## Adding New Tools

1. Add business logic to `HeadlessIdeMcp.Core`
2. Create a tool class in `HeadlessIdeMcp.Server` with `[McpServerToolType]` attribute
3. Mark methods with `[McpServerTool]` attribute
4. Add integration tests
5. Update the `.http` file with example requests

See [Project Setup](docs/project-setup.md) for detailed instructions.

## Contributing

Contributions are welcome! Please ensure:
- All tests pass (`dotnet test`)
- Integration tests cover new functionality
- Documentation is updated
- Docker build succeeds

## License

See [LICENCE.md](LICENCE.md)

---

[docs :open_book:](https://dazinator.github.io/headless-ide-mcp/)


### Serving the Docs Locally

Make sure you have python 3 installed, then run the following commands in the repo root directory (you may have to run as administrator)

```sh
  pip install --upgrade pip setuptools wheel
  pip install -r docs/requirements.txt 
```

You can now build the docs site, and start the mkdocs server for live preview:

```
mkdocs build
mkdocs serve
```
or
```
mike deploy local
mike set-default local
mike serve
```

Browse to the docs site on `http://127.0.0.1:8000/` - the site will reload as you make changes.

For more information including features, see [mkdocs-material](https://squidfunk.github.io/mkdocs-material/)
