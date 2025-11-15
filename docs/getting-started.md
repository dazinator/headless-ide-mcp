# Getting Started with Headless IDE MCP

This guide will help you get started with the Headless IDE Model Context Protocol (MCP) server.

## Overview

The Headless IDE MCP server is an ASP.NET Core application that provides MCP tools for analyzing .NET codebases. It uses the [ModelContextProtocol.AspNetCore](https://www.nuget.org/packages/ModelContextProtocol.AspNetCore) library to expose tools that can be consumed by AI assistants and other MCP clients.

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (for containerized deployment)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) (optional, for debugging with Docker Compose)

## Project Structure

```
headless-ide-mcp/
├── src/
│   ├── HeadlessIdeMcp.Server/          # ASP.NET Core MCP server
│   ├── HeadlessIdeMcp.Core/            # Core tool logic
│   ├── HeadlessIdeMcp.IntegrationTests/ # Integration tests
│   └── Solution.sln
├── sample-codebase/                     # Sample .NET solution for testing
│   ├── SampleProject1/
│   ├── SampleProject2/
│   └── SampleCodeBase.sln
├── docker-compose.yml                   # Docker Compose configuration
├── docker-compose.dcproj                # Visual Studio Docker Compose project
└── Dockerfile                           # Container image definition
```

## Running Locally

### Option 1: Run with .NET CLI

1. Navigate to the server project:
   ```bash
   cd src/HeadlessIdeMcp.Server
   ```

2. Set the code base path environment variable:
   ```bash
   export CODE_BASE_PATH=/path/to/sample-codebase
   ```

3. Run the application:
   ```bash
   dotnet run
   ```

4. The server will start on `http://localhost:5000`

### Option 2: Run with Docker Compose

1. Build and start the container:
   ```bash
   docker-compose up --build
   ```

2. The server will be available at `http://localhost:5000`

3. The sample codebase is automatically mounted at `/workspace` in the container

### Option 3: Debug with Visual Studio 2022

1. Open the solution in Visual Studio 2022
2. Set `docker-compose` as the startup project
3. Press F5 to start debugging
4. Visual Studio will build the Docker image and start the container with debugging enabled

## Testing the MCP Server

### Using the .http File

A `.http/test-mcp-server.http` file is provided in the root directory. You can use it with:
- Visual Studio 2022's built-in HTTP client
- Visual Studio Code with the REST Client extension
- JetBrains Rider

Example requests:

**Health Check:**
```http
GET http://localhost:5000/health
```

**List Available Tools:**
```http
POST http://localhost:5000/
Content-Type: application/json

{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/list"
}
```

**Call CheckFileExists Tool:**
```http
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

## Available MCP Tools

### check_file_exists

Checks if a specific file exists in the code base.

**Parameters:**
- `fileName` (string): The file path to check (can be relative to the code base or absolute)

**Returns:**
- A message indicating whether the file exists

**Example:**
```json
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

## Running Tests

### Unit and Integration Tests

Run all tests:
```bash
cd src
dotnet test
```

Run integration tests only:
```bash
cd src/HeadlessIdeMcp.IntegrationTests
dotnet test
```

The integration tests run against the actual file system with no mocked dependencies, providing true end-to-end validation of the tools.

## Building the Docker Image

To build the Docker image manually:

```bash
docker build -t headless-ide-mcp:dev .
```

## Configuration

### Environment Variables

- `CODE_BASE_PATH`: The base path for the code to analyze (default: `/workspace` in Docker, current directory otherwise)
- `ASPNETCORE_ENVIRONMENT`: The ASP.NET Core environment (Development, Production, etc.)
- `ASPNETCORE_HTTP_PORTS`: HTTP port (default: 8080 in container, 5000 on host)

## Next Steps

- Explore the sample codebase at `sample-codebase/` to understand what the tools can analyze
- Review the integration tests at `src/HeadlessIdeMcp.IntegrationTests/` to see how tools are tested
- Add new tools by creating classes in `HeadlessIdeMcp.Server` with the `[McpServerToolType]` attribute
- Extend the `HeadlessIdeMcp.Core` library with additional analysis logic

## Troubleshooting

### Container cannot access mounted volume

If the container cannot see the sample codebase:
1. Check that Docker Desktop has file sharing enabled for the project directory
2. Verify the volume mount path in `docker-compose.yml`
3. Check container logs: `docker-compose logs headless-ide-mcp`

### MCP endpoint returns 404

The MCP server exposes endpoints at the root path `/` by default. Ensure you're making requests to the root endpoint with proper JSON-RPC format. Check the `.http` file for examples.

### Build failures

1. Ensure .NET 8.0 SDK is installed
2. Run `dotnet restore` in the `src/` directory
3. Check that all NuGet packages are restored successfully
