# Headless IDE MCP Bridge

A lightweight .NET console application that acts as a bridge between Claude Desktop (stdio transport) and the Headless IDE MCP Server (HTTP/SSE transport).

## Purpose

Claude Desktop only supports MCP servers that communicate over **stdio**, not HTTP. This bridge solves that problem by:

- Running locally as a stdio-based MCP server that Claude Desktop can launch
- Forwarding all MCP requests to the Headless IDE MCP Server running in Docker via HTTP/SSE
- Proxying responses back to Claude Desktop over stdio
- Eliminating the need for Node.js or external npm packages

## Usage

### Basic Usage

```bash
# Connect to local MCP server (HTTP)
headless-ide-mcp-bridge http://localhost:5000/

# Connect to local MCP server (HTTPS)
headless-ide-mcp-bridge https://localhost:5001/

# Connect to remote MCP server
headless-ide-mcp-bridge https://myserver.example.com/
```

The bridge reads JSON-RPC messages from stdin and writes responses to stdout, following the MCP protocol specification.

### With Claude Desktop

Configure Claude Desktop to launch the bridge as an MCP server:

**Windows:**
```json
{
  "mcpServers": {
    "headless-ide": {
      "command": "C:\\path\\to\\headless-ide-mcp-bridge.exe",
      "args": ["http://localhost:5000/"]
    }
  }
}
```

**macOS/Linux:**
```json
{
  "mcpServers": {
    "headless-ide": {
      "command": "/path/to/headless-ide-mcp-bridge",
      "args": ["http://localhost:5000/"]
    }
  }
}
```

## Building from Source

```bash
cd src/HeadlessIdeMcp.Bridge
dotnet build
```

## Publishing

Create a self-contained executable for your platform:

### Windows (x64)
```bash
dotnet publish -c Release -r win-x64 --self-contained
```

### macOS (ARM64 - M1/M2/M3)
```bash
dotnet publish -c Release -r osx-arm64 --self-contained
```

### macOS (x64 - Intel)
```bash
dotnet publish -c Release -r osx-x64 --self-contained
```

### Linux (x64)
```bash
dotnet publish -c Release -r linux-x64 --self-contained
```

The executable will be in `bin/Release/net8.0/{runtime}/publish/`

## How It Works

1. **Stdin → Bridge:** Claude Desktop sends JSON-RPC messages via stdin
2. **Bridge → Server:** Bridge forwards messages to MCP server via HTTP POST
3. **Server → Bridge:** MCP server responds with SSE (Server-Sent Events) format
4. **Bridge → Stdout:** Bridge extracts JSON from SSE and writes to stdout

The bridge automatically:
- Adds required HTTP headers (`Accept: application/json, text/event-stream`)
- Parses SSE response format and extracts JSON data
- Handles errors and returns proper JSON-RPC error responses
- Logs diagnostic information to stderr (not stdout, to avoid interfering with protocol)

## Architecture

```
┌─────────────────┐
│ Claude Desktop  │
│    (stdio)      │
└────────┬────────┘
         │ JSON-RPC over stdin/stdout
         ▼
┌─────────────────┐
│  MCP Bridge     │
│  (this app)     │
└────────┬────────┘
         │ HTTP/SSE
         ▼
┌─────────────────┐
│  MCP Server     │
│  (Docker/HTTP)  │
└─────────────────┘
```

## Logging

The bridge logs diagnostic messages to **stderr** to avoid interfering with the MCP protocol on stdout. To see the logs:

```bash
# Redirect stderr to a file
headless-ide-mcp-bridge http://localhost:5000/ 2> bridge.log
```

Sample log output:
```
[MCP Bridge] Starting MCP bridge to http://localhost:5000/
[MCP Bridge] Server health check passed
[MCP Bridge] Bridge ready - listening for MCP messages on stdin
[MCP Bridge] Received message from stdin: {"jsonrpc":"2.0","id":1,"method":"initialize"...
[MCP Bridge] Received response from server (214 bytes)
[MCP Bridge] Extracted JSON from SSE: {"result":{"protocolVersion":"2024-11-05"...
```

## Requirements

- .NET 8.0 Runtime (if using self-contained builds, no runtime needed)
- Access to Headless IDE MCP Server (running locally or remotely)

## Troubleshooting

### Bridge doesn't start
- Ensure the server URL is correct and ends with `/`
- Check that the MCP server is running and accessible
- Verify network connectivity to the server

### Connection errors
- Check the bridge stderr logs for detailed error messages
- Verify the server health endpoint: `curl http://localhost:5000/health`
- Ensure no firewall is blocking the connection

### Claude Desktop shows "Server disconnected"
- Check that Claude Desktop configuration points to the correct bridge executable
- Verify the server URL in the args is correct
- Review Claude Desktop logs (Settings → Developer → View Logs)

## License

See the [main project LICENSE](../../LICENCE.md)
