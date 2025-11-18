# Testing the MCP Bridge

This document describes how to test the MCP Bridge locally.

## Prerequisites

1. Build the bridge:
   ```bash
   cd src/HeadlessIdeMcp.Bridge
   dotnet build
   ```

2. Start the MCP server:
   ```bash
   cd src/HeadlessIdeMcp.Server
   CODE_BASE_PATH=/path/to/sample-codebase dotnet run
   ```
   
   The server will start on `http://localhost:8080` and `https://localhost:8081`

## Manual Testing

### Test 1: Initialize Connection

```bash
echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0.0"}}}' | \
  ./bin/Debug/net8.0/linux-x64/headless-ide-mcp-bridge http://localhost:8080/ 2>/dev/null | jq .
```

**Expected Output:**
```json
{
  "result": {
    "protocolVersion": "2024-11-05",
    "capabilities": {
      "logging": {},
      "tools": {
        "listChanged": true
      }
    },
    "serverInfo": {
      "name": "HeadlessIdeMcp.Server",
      "version": "1.0.0.0"
    }
  },
  "id": 1,
  "jsonrpc": "2.0"
}
```

### Test 2: List Tools

```bash
echo '{"jsonrpc":"2.0","id":2,"method":"tools/list"}' | \
  ./bin/Debug/net8.0/linux-x64/headless-ide-mcp-bridge http://localhost:8080/ 2>/dev/null | jq .
```

**Expected Output:**
```json
{
  "result": {
    "tools": [
      {
        "name": "shell_get_available_tools",
        "description": "Get a list of available CLI tools in the container environment",
        ...
      },
      {
        "name": "shell_execute_json",
        "description": "Execute a CLI command that returns JSON output. Automatically parses the JSON response.",
        ...
      },
      {
        "name": "shell_execute",
        "description": "Execute a CLI command in a sandboxed environment. Returns stdout, stderr, and exit code.",
        ...
      },
      {
        "name": "check_file_exists",
        "description": "Checks if a specific file exists in the code base",
        ...
      }
    ]
  },
  "id": 2,
  "jsonrpc": "2.0"
}
```

### Test 3: Call a Tool

```bash
echo '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"shell_get_available_tools","arguments":{}}}' | \
  ./bin/Debug/net8.0/linux-x64/headless-ide-mcp-bridge http://localhost:8080/ 2>/dev/null | jq .
```

**Expected Output:**
```json
{
  "result": {
    "content": [
      {
        "type": "text",
        "text": "Available tools:\n- dotnet (version: 8.0.x)\n- git (version: 2.x.x)\n..."
      }
    ]
  },
  "id": 3,
  "jsonrpc": "2.0"
}
```

## Viewing Bridge Logs

The bridge logs diagnostic information to stderr. To view the logs:

```bash
echo '{"jsonrpc":"2.0","id":1,"method":"tools/list"}' | \
  ./bin/Debug/net8.0/linux-x64/headless-ide-mcp-bridge http://localhost:8080/ 2>&1 | grep "MCP Bridge"
```

**Expected Log Output:**
```
[MCP Bridge] Starting MCP bridge to http://localhost:8080/
[MCP Bridge] Server health check passed
[MCP Bridge] Bridge ready - listening for MCP messages on stdin
[MCP Bridge] Received message from stdin: {"jsonrpc":"2.0","id":1,"method":"tools/list"}...
[MCP Bridge] Received response from server (XXX bytes)
[MCP Bridge] Extracted JSON from SSE: {"result":{"tools":[...
[MCP Bridge] stdin closed, shutting down bridge
```

## Testing with Claude Desktop

See the [Claude Desktop Setup Guide](../../docs/claude-desktop-setup.md) for instructions on configuring Claude Desktop to use the bridge.

## Publishing and Testing

To test the published single-file executable:

1. Publish the bridge:
   ```bash
   dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true
   ```

2. Test the published executable:
   ```bash
   echo '{"jsonrpc":"2.0","id":1,"method":"tools/list"}' | \
     ./bin/Release/net8.0/linux-x64/publish/headless-ide-mcp-bridge http://localhost:8080/ 2>/dev/null | jq .
   ```

## Troubleshooting

### Bridge doesn't connect to server

1. Verify the server is running:
   ```bash
   curl http://localhost:8080/health
   ```

2. Check bridge stderr output for error messages

3. Ensure the server URL is correct and ends with `/`

### Invalid JSON output

1. Check if the server is returning SSE format - the bridge should handle this automatically

2. Verify the bridge is parsing SSE correctly by checking stderr logs

3. Test the server directly with curl:
   ```bash
   curl -X POST http://localhost:8080/ \
     -H "Content-Type: application/json" \
     -H "Accept: application/json, text/event-stream" \
     -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
   ```

## Automated Test Script

A comprehensive test script is available at `src/HeadlessIdeMcp.Bridge/test-bridge.sh` (create if needed):

```bash
#!/bin/bash
# Automated test script for the MCP Bridge

set -e

# Start server
cd ../HeadlessIdeMcp.Server
CODE_BASE_PATH=../../sample-codebase dotnet run &
SERVER_PID=$!
sleep 5

# Run tests
cd ../HeadlessIdeMcp.Bridge

echo "Testing bridge..."
tests=(
    '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0.0"}}}'
    '{"jsonrpc":"2.0","id":2,"method":"tools/list"}'
    '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"shell_get_available_tools","arguments":{}}}'
)

for test in "${tests[@]}"; do
    echo "$test" | ./bin/Debug/net8.0/linux-x64/headless-ide-mcp-bridge http://localhost:8080/ 2>/dev/null | jq -e . > /dev/null
    echo "✓ Test passed"
done

# Cleanup
kill $SERVER_PID
echo "All tests completed successfully!"
```
