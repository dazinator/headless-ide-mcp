# Connecting Claude Desktop to Headless IDE MCP

This guide explains how to configure Claude Desktop to connect to the Headless IDE MCP server running in a Docker container.

## Overview

The Headless IDE MCP server uses **HTTP transport** and runs as a containerized service. Claude Desktop provides two ways to connect:

1. **✅ Recommended: Remote MCP Connector** (Beta) - Direct HTTP connection using Claude's native remote connector feature
2. **Alternative: stdio Bridge Proxy** - Uses a bridge tool to convert between stdio and HTTP

## Method 1: Remote MCP Connector (Recommended)

**⚠️ IMPORTANT LIMITATION**: Claude Desktop's remote connector currently **requires HTTPS** for security. The default container configuration only supports HTTP, which means this method won't work out of the box.

**Status:** This method is recommended for its simplicity, but requires HTTPS support to be added to the container first. See [Issue #TBD] for HTTPS support progress.

**Claude Desktop supports direct HTTPS connections to remote MCP servers** through its "Custom Connectors" feature (currently in Beta). Once HTTPS is configured, this is the simplest approach.

### Prerequisites

- Claude Desktop installed ([download here](https://claude.ai/download))
- Docker Desktop running
- **Headless IDE MCP server with HTTPS enabled** (⚠️ not yet implemented - see limitation above)

### Current Limitation

The remote connector will show the error: **"URL must start with 'https'"** when trying to use `http://localhost:5000/`.

To use this method, the container needs to be configured with:
- HTTPS certificate (development or production)
- HTTPS port exposed and configured
- Proper certificate mounting in Docker

**For now, use [Method 2: stdio Bridge Proxy](#method-2-stdio-bridge-proxy-alternative)** which doesn't have this HTTPS requirement.

### Step 1: Start the Headless IDE MCP Server (When HTTPS is Available)

Ensure the MCP server is running with Docker Compose:

```bash
docker-compose up --build
```

The server will be available at `https://localhost:5001` (when HTTPS is configured)

Verify it's running:
```bash
curl https://localhost:5001/health --insecure
```

You should see:
```json
{"status":"healthy","codeBasePath":"/workspace"}
```

### Step 2: Add Remote Connector in Claude Desktop (When HTTPS is Available)

1. Open Claude Desktop
2. Go to **Settings** → **Developer** (or **Integrations**)
3. Click **"Add custom connector"** or **"Add MCP Server"**
4. In the dialog:
   - **Name**: `Headless IDE` (or any name you prefer)
   - **Remote MCP server URL**: `https://localhost:5001/` (**must be HTTPS**)
5. Click **"Add"**

Claude Desktop will connect directly to your HTTPS MCP server - no bridge needed!

### Step 3: Verify the Connection (When HTTPS is Available)

1. Start a new conversation in Claude Desktop
2. Look for the tool icon (🔧) or check if MCP tools are available
3. Try using one of the Headless IDE tools:
   - "Can you check if the file `SampleProject1/Calculator.cs` exists?"
   - "What tools are available in the shell environment?"
   - "Run `dotnet --version` in the workspace"

**That's it!** The remote connector handles the HTTPS communication natively.

**Benefits of this method (when HTTPS is available):**
- ✅ No bridge proxy needed
- ✅ No Node.js installation required
- ✅ Direct HTTPS connection
- ✅ Simpler configuration
- ✅ Native Claude Desktop feature

---

## Method 2: stdio Bridge Proxy (Currently Recommended)

**✅ Use this method** - It's currently the only working option since:
- The container doesn't support HTTPS yet (required for Method 1)
- The bridge connects to HTTP locally, bypassing Claude's HTTPS requirement

**Note**: There are known protocol compatibility issues between some stdio-to-HTTP bridges and the ASP.NET Core MCP implementation. If the bridge doesn't work, HTTPS support needs to be added to use Method 1.

### Architecture

### Architecture

```
Claude Desktop (stdio) 
    ↓
mcp-server-and-gw (bridge proxy)
    ↓
Headless IDE MCP Server (HTTP) in Docker Container
```

### Prerequisites

- **Claude Desktop** installed ([download here](https://claude.ai/download))
- **Docker Desktop** running
- **Node.js (v18 or later)** and **npm** installed - Required for the bridge proxy
  - Download from [nodejs.org](https://nodejs.org/)
  - **Windows users**: After installation, restart your terminal/command prompt
  - Verify installation by running: `node -v` and `npm -v`
- **Headless IDE MCP server** running in Docker

### Step 1: Start the Headless IDE MCP Server

First, ensure the MCP server is running with Docker Compose:

```bash
docker-compose up --build
```

The server will be available at `http://localhost:5000`

Verify it's running:
```bash
curl http://localhost:5000/health
```

You should see:
```json
{"status":"healthy","codeBasePath":"/workspace"}
```

### Step 2: Configure Claude Desktop

**Before proceeding**: Verify that Node.js and npx are installed by running:
- **Windows (PowerShell)**: `node -v; npm -v; npx -v`
- **macOS/Linux**: `node -v && npm -v && npx -v`

If any command is not found, install Node.js from [nodejs.org](https://nodejs.org/) first. See the [Troubleshooting section](#npx-is-not-recognized-error-on-windows) if needed.

#### Locate the Configuration File

The Claude Desktop configuration file location depends on your operating system:

- **macOS**: `~/Library/Application Support/Claude/claude_desktop_config.json`
- **Windows**: `%APPDATA%\Claude\claude_desktop_config.json`
- **Linux**: `~/.config/Claude/claude_desktop_config.json`

**Tip**: You can also access this file via Claude Desktop's menu: `Settings > Developer > Edit Config`

#### Add the MCP Server Configuration

Edit the `claude_desktop_config.json` file and add the following configuration.

#### For Windows

On Windows, you must wrap `npx` with `cmd /c` because `npx` is not a native executable:

```json
{
  "mcpServers": {
    "headless-ide": {
      "command": "cmd",
      "args": [
        "/c",
        "npx",
        "-y",
        "mcp-server-and-gw",
        "http://localhost:5000/"
      ]
    }
  }
}
```

#### For macOS and Linux

```json
{
  "mcpServers": {
    "headless-ide": {
      "command": "npx",
      "args": [
        "-y",
        "mcp-server-and-gw",
        "http://localhost:5000/"
      ]
    }
  }
}
```

**Configuration Breakdown:**
- `headless-ide`: A friendly name for your MCP server (you can choose any name)
- `command`: 
  - **Windows**: `cmd` with `/c` flag to execute the npx command
  - **macOS/Linux**: `npx` to run the bridge proxy on-the-fly
- `args`: 
  - **Windows**: Starts with `/c`, `npx`
  - `-y`: Auto-confirms package installation
  - `mcp-server-and-gw`: The stdio-to-HTTP bridge package
  - `http://localhost:5000/`: The URL of your MCP server

### Alternative: Using a Pre-installed Bridge

If you prefer to install the bridge proxy globally:

```bash
npm install -g mcp-server-and-gw
```

Then configure Claude Desktop to use it directly.

**For Windows:**
```json
{
  "mcpServers": {
    "headless-ide": {
      "command": "cmd",
      "args": ["/c", "mcp-server-and-gw", "http://localhost:5000/"]
    }
  }
}
```

**For macOS/Linux:**
```json
{
  "mcpServers": {
    "headless-ide": {
      "command": "mcp-server-and-gw",
      "args": ["http://localhost:5000/"]
    }
  }
}
```

### Step 3: Restart Claude Desktop

After saving the configuration file, completely quit and restart Claude Desktop for the changes to take effect.

### Step 4: Verify the Connection

1. Open Claude Desktop
2. Start a new conversation
3. Look for the tool icon (🔧) or check if MCP tools are available
4. Try using one of the Headless IDE tools:

**Example prompts to test:**
- "Can you check if the file `SampleProject1/Calculator.cs` exists?"
- "What tools are available in the shell environment?"
- "Run `dotnet --version` in the workspace"

If configured correctly, Claude will use the MCP tools from your containerized server.

---

## Troubleshooting

### Remote Connector Not Available

**Problem**: You don't see the "Add custom connector" or "Remote MCP server URL" option in Claude Desktop settings.

**Solution**: This feature is currently in Beta and may not be available in all versions or regions. If you don't have access:
- Update to the latest version of Claude Desktop
- Use [Method 2: stdio Bridge Proxy](#method-2-stdio-bridge-proxy-alternative) as an alternative

### Connection Failed with Remote Connector

**Problem**: After adding the remote connector, Claude Desktop shows connection errors.

**Solutions**:
1. **Verify the container is running**:
   ```bash
   docker ps
   ```
   Look for `headless-ide-mcp-server`.

2. **Test the health endpoint**:
   ```bash
   curl http://localhost:5000/health
   ```
   Should return: `{"status":"healthy","codeBasePath":"/workspace"}`

3. **Check the URL format** - must end with `/`: `https://localhost:5001/` (when HTTPS is available)

4. **Restart both** the Docker container and Claude Desktop

### "URL must start with 'https'" Error

**Problem**: When adding a remote connector, Claude Desktop shows: **"URL must start with 'https'"**

**Cause**: Claude Desktop's remote connector requires HTTPS for security. The current container only supports HTTP.

**Solution**: 

**Short-term**: Use [Method 2 (stdio Bridge Proxy)](#method-2-stdio-bridge-proxy-currently-recommended) which doesn't require HTTPS.

**Long-term**: The container needs HTTPS support added. This requires:
1. Development certificate generated and configured
2. Certificate mounted in Docker container
3. HTTPS port exposed (5001)
4. Program.cs configured for HTTPS URLs

Track progress on adding HTTPS support in the repository issues.

### "npx is not recognized" Error on Windows

**Note**: This only applies to Method 2 (stdio Bridge Proxy).

**Problem**: When trying to run `npx` commands, you see "'npx' is not recognized as an internal or external command" or Claude Desktop shows "Server disconnected" errors.

**Cause**: Node.js and npm are not installed or not in your system PATH.

**Solution**:

1. **Install Node.js** (includes npm and npx):
   - Download the LTS version from [nodejs.org](https://nodejs.org/)
   - Run the installer and ensure "Add to PATH" is selected
   - **Important**: Restart your terminal/command prompt after installation

2. **Verify Installation**:
   Open a new Command Prompt or PowerShell window and run:
   ```powershell
   node -v
   npm -v
   npx -v
   ```
   You should see version numbers for each command.

3. **If commands still aren't recognized**:
   - Verify Node.js is in your PATH:
     - Open "Environment Variables" in Windows Settings
     - Check that `C:\Program Files\nodejs\` is in your PATH
   - Restart your computer if needed
   - Try running Command Prompt as Administrator

4. **After Node.js is installed**, restart Claude Desktop and the MCP server should connect successfully.

### "Cannot read properties of undefined" Error on Windows

**Problem**: On Windows, you see an error like "Cannot read properties of undefined (reading 'cmd')" or "MCP dev_buddy: Cannot read properties of undefined".

**Solution**: This occurs because `npx` is not a native Windows executable. You must use `cmd /c` to run it:

**Incorrect (will fail on Windows):**
```json
{
  "mcpServers": {
    "dev_buddy": {
      "command": "npx",
      "args": ["-y", "mcp-server-and-gw", "http://localhost:5000/"]
    }
  }
}
```

**Correct (Windows):**
```json
{
  "mcpServers": {
    "dev_buddy": {
      "command": "cmd",
      "args": ["/c", "npx", "-y", "mcp-server-and-gw", "http://localhost:5000/"]
    }
  }
}
```

The `/c` flag tells `cmd` to execute the command and then terminate. This properly spawns the `npx` process on Windows.

### "Request timed out" / "Server disconnected" Error

**Problem**: Claude Desktop logs show the bridge connects successfully but then displays:
- "MCP error -32001: Request timed out"
- "Server disconnected"
- Bridge logs show: `--- SSE backend connected` but then times out

**Cause**: The MCP server container is either not running, not accessible, or not responding to requests.

**Solution**:

1. **Verify the Docker container is actually running**:
   ```bash
   docker ps
   ```
   Look for `headless-ide-mcp-server` in the list. If it's not there, start it:
   ```bash
   docker-compose up -d
   ```

2. **Check the MCP server is responding**:
   ```bash
   curl http://localhost:5000/health
   ```
   You should see: `{"status":"healthy","codeBasePath":"/workspace"}`
   
   If you get "Connection refused" or no response:
   - The container isn't running (see step 1)
   - Check Docker logs: `docker-compose logs headless-ide-mcp`

3. **Test the MCP server directly** with a tools/list request:
   
   **PowerShell:**
   ```powershell
   Invoke-WebRequest -Uri "http://localhost:5000/" -Method POST -ContentType "application/json" -Body '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
   ```
   
   **Command Prompt / macOS / Linux:**
   ```bash
   curl -X POST http://localhost:5000/ \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
   ```
   
   You should get a JSON response listing available tools.
   
   If this fails, the MCP server has an issue - check container logs.

4. **Verify the bridge can connect** (test manually):
   
   **Windows (Command Prompt):**
   ```cmd
   npx -y mcp-server-and-gw http://localhost:5000/
   ```
   
   **macOS/Linux:**
   ```bash
   npx -y mcp-server-and-gw http://localhost:5000/
   ```
   
   You should see:
   ```
   -- Connecting to MCP server at http://localhost:5000
   --- SSE backend connected
   -- MCP stdio to SSE gateway running - connected to http://localhost:5000
   ```
   
   If it connects but then times out, the MCP server is running but not responding to MCP protocol messages. See the "Message Processing Canceled" section below.

5. **Restart everything in order**:
   - Stop Claude Desktop completely (not just close - actually quit)
   - Restart the Docker container: `docker-compose restart`
   - Wait 10 seconds for the server to fully start
   - Verify health endpoint works: `curl http://localhost:5000/health`
   - Start Claude Desktop
   - Open Developer Settings to watch the logs

### "Message Processing Canceled" Error

**Problem**: Health endpoint works (`http://localhost:5000/health` returns healthy status), but container logs show:
```
Server (HeadlessIdeMcp.Server 1.0.0.0) message processing canceled.
```

**Cause**: The MCP server is receiving requests but the HTTP/SSE transport is not completing the handshake properly. This can happen when:
- The bridge is sending requests to the wrong endpoint
- There's a protocol version mismatch
- The server is rejecting the connection

**Solution**:

1. **Verify you're using the correct URL format** - it must end with a `/`:
   ```json
   {
     "mcpServers": {
       "dev_buddy": {
         "command": "cmd",
         "args": ["/c", "npx", "-y", "mcp-server-and-gw", "http://localhost:5000/"]
       }
     }
   }
   ```
   Note the trailing `/` in `http://localhost:5000/` - this is required.

2. **Test the tools/list endpoint works**:
   
   **PowerShell:**
   ```powershell
   $response = Invoke-WebRequest -Uri "http://localhost:5000/" -Method POST -ContentType "application/json" -Body '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
   $response.Content
   ```
   
   You should see a JSON response with `"result":{"tools":[...]}` listing available tools.

3. **Test the bridge directly** to isolate if it's a protocol compatibility issue:
   
   Run the bridge manually and send it an initialize message via stdin:
   
   **PowerShell:**
   ```powershell
   # Start the bridge in the background
   $bridge = Start-Process -FilePath "npx" -ArgumentList "-y", "mcp-server-and-gw", "http://localhost:5000/" -NoNewWindow -PassThru -RedirectStandardInput "bridge-input.txt" -RedirectStandardOutput "bridge-output.txt" -RedirectStandardError "bridge-error.txt"
   
   # Send an initialize message
   '{"jsonrpc":"2.0","id":0,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0.0"}}}' | Out-File -FilePath "bridge-input.txt" -Encoding UTF8
   
   # Wait a few seconds
   Start-Sleep -Seconds 5
   
   # Check the output
   Get-Content "bridge-output.txt"
   Get-Content "bridge-error.txt"
   
   # Kill the bridge
   Stop-Process -Id $bridge.Id
   ```
   
   **Command Prompt / Bash:**
   ```bash
   # Start the bridge and send test message
   echo '{"jsonrpc":"2.0","id":0,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0.0"}}}' | npx -y mcp-server-and-gw http://localhost:5000/
   ```
   
   **Expected behavior:**
   - Bridge should connect to the server
   - Bridge should send the initialize message
   - You should see a response from the server
   - Bridge should wait for more stdin input
   
   **If the bridge times out or disconnects**, this confirms a protocol compatibility issue. The problem could be:
   - The bridge expects a different MCP protocol version than the server provides
   - The SSE implementation between bridge and server are incompatible
   - The bridge cannot parse the server's SSE response format

4. **Check if the bridge is using the correct protocol**:
   - The `mcp-server-and-gw` bridge expects an SSE endpoint
   - Verify the server is exposing SSE by checking the response headers include `text/event-stream`

5. **Try testing with the .http file** to verify MCP protocol works:
   - Check the `.http/test-mcp-server.http` file in the repository
   - Use VS Code with REST Client extension or similar to test the endpoints
   - Verify `tools/list` and `tools/call` methods work

6. **If the bridge test fails**, this may indicate a compatibility issue between:
   - The MCP server implementation (ModelContextProtocol.AspNetCore)
   - The bridge proxy (mcp-server-and-gw)
   - Claude Desktop's expected protocol version
   
   **Alternative approaches:**
   - Try a different bridge/gateway implementation
   - Check if there's a version mismatch in MCP protocol versions
   - Consider filing an issue with the `mcp-server-and-gw` project about ASP.NET Core compatibility
   
   Check:
   - Docker container logs: `docker-compose logs -f headless-ide-mcp`
   - Whether the server needs to be rebuilt: `docker-compose up --build`

### Connection Issues

**Problem**: Claude Desktop shows "MCP server failed to start" or similar error.

**Solutions**:
1. Verify the Docker container is running:
   ```bash
   docker ps | grep headless-ide-mcp
   ```

2. Check the MCP server is accessible:
   ```bash
   curl http://localhost:5000/health
   ```

3. Test the bridge proxy manually:
   
   **Windows (PowerShell):**
   ```powershell
   npx -y mcp-server-and-gw http://localhost:5000/
   ```
   
   **macOS/Linux:**
   ```bash
   npx -y mcp-server-and-gw http://localhost:5000/
   ```
   This should start the proxy and wait for stdin input. Press `Ctrl+C` to exit.

4. Check Claude Desktop logs (if available in Settings > Developer)

### Port Conflicts

If port 5000 is already in use on your system, you can change the port mapping in `docker-compose.yml`:

```yaml
ports:
  - "5100:8080"  # Change 5000 to 5100 or another available port
```

Then update your Claude Desktop configuration to use the new port.

**Windows:**
```json
{
  "mcpServers": {
    "headless-ide": {
      "command": "cmd",
      "args": ["/c", "npx", "-y", "mcp-server-and-gw", "http://localhost:5100/"]
    }
  }
}
```

**macOS/Linux:**
```json
{
  "mcpServers": {
    "headless-ide": {
      "command": "npx",
      "args": ["-y", "mcp-server-and-gw", "http://localhost:5100/"]
    }
  }
}
```

### Authentication Issues

If you've enabled API key authentication on the MCP server (see [Authentication Documentation](authentication.md)), you'll need to configure the bridge to pass the API key.

Currently, basic HTTP headers can be added through environment variables. Check the `mcp-server-and-gw` documentation for the latest authentication options.

### Tools Not Appearing

1. Ensure the MCP server is returning the tools list:
   ```bash
   curl -X POST http://localhost:5000/ \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
   ```

2. Verify you're using a compatible version of Claude Desktop with MCP support

3. Check that the `mcpServers` configuration is valid JSON (no trailing commas, proper quotes)

## Available Tools

Once connected, the following MCP tools will be available in Claude Desktop:

### File System Tools
- `check_file_exists`: Check if a file exists in the codebase

### Shell Execution Tools
- `shell_execute`: Execute CLI commands (dotnet, git, rg, jq, etc.)
- `shell_execute_json`: Execute commands that return JSON output
- `shell_get_available_tools`: List available CLI tools in the container

For detailed tool documentation, see the [main README](../README.md#available-mcp-tools).

## Security Considerations

### Network Security

When running the MCP server:
- The default configuration exposes the server on `localhost:5000`
- This is accessible to any process on your machine
- If you need to restrict access, consider:
  - Using Docker network isolation
  - Enabling API key authentication (see [Authentication](authentication.md))
  - Using firewall rules to limit access

### Container Security

The Headless IDE MCP server runs with production-grade security:
- Non-root user execution
- Capability dropping
- Resource limits (CPU and memory)
- Command allowlist/denylist
- Path restrictions
- Comprehensive audit logging

For more details, see the [Security Documentation](security.md).

## Advanced Configuration

### Custom Workspace Path

To analyze a different codebase, modify the volume mount in `docker-compose.yml`:

```yaml
volumes:
  - /path/to/your/codebase:/workspace
```

Then restart the container:
```bash
docker-compose down
docker-compose up --build
```

### Multiple MCP Servers

You can configure multiple MCP servers in Claude Desktop.

**Windows:**
```json
{
  "mcpServers": {
    "headless-ide": {
      "command": "cmd",
      "args": ["/c", "npx", "-y", "mcp-server-and-gw", "http://localhost:5000/"]
    },
    "another-server": {
      "command": "cmd",
      "args": ["/c", "npx", "-y", "some-other-mcp-server"]
    }
  }
}
```

**macOS/Linux:**
```json
{
  "mcpServers": {
    "headless-ide": {
      "command": "npx",
      "args": ["-y", "mcp-server-and-gw", "http://localhost:5000/"]
    },
    "another-server": {
      "command": "npx",
      "args": ["-y", "some-other-mcp-server"]
    }
  }
}
```

### Running in Production

For production deployments:

1. Use a proper reverse proxy (nginx, Caddy) with TLS
2. Enable API key authentication
3. Set `ASPNETCORE_ENVIRONMENT=Production` in docker-compose.yml
4. Review the [Security Checklist](security-checklist.md)
5. Monitor logs as described in the [Operations Guide](operations.md)

## Alternative: Direct HTTP Access

**Note**: Claude Desktop does not currently support direct HTTP/SSE transport natively. The bridge proxy method described above is the recommended approach for connecting Claude Desktop to HTTP-based MCP servers.

If you're building your own MCP client or using a different AI assistant that supports HTTP transport, you can connect directly to `http://localhost:5000/` using JSON-RPC messages. See the [.http test file](../.http/test-mcp-server.http) for examples.

## Related Documentation

- [Getting Started Guide](getting-started.md) - General setup and usage
- [Authentication](authentication.md) - API key configuration
- [Security](security.md) - Security features and best practices
- [Operations](operations.md) - Monitoring and maintenance
- [Main README](../README.md) - Project overview and tool documentation

## Getting Help

If you encounter issues:

1. Check the [Troubleshooting](#troubleshooting) section above
2. Review the Docker container logs: `docker-compose logs headless-ide-mcp`
3. Test the MCP server directly with curl (see examples in the [.http file](../.http/test-mcp-server.http))
4. Open an issue on the [GitHub repository](https://github.com/dazinator/headless-ide-mcp/issues)

## References

- [Model Context Protocol Specification](https://modelcontextprotocol.io/)
- [Claude Desktop User Guide](https://docs.anthropic.com/claude/docs)
- [mcp-server-and-gw Bridge Tool](https://github.com/boilingdata/mcp-server-and-gw)
