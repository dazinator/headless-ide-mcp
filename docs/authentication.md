# Authentication and Concurrent Usage

## API Key Authentication

The Headless IDE MCP server supports optional API key authentication for securing access to the MCP endpoints.

### Configuration

API key authentication is disabled by default but can be enabled through configuration:

**appsettings.json:**
```json
{
  "Authentication": {
    "ApiKey": {
      "Enabled": true,
      "Key": "your-secret-api-key-here"
    }
  }
}
```

**Environment Variables:**
```bash
# Enable API key authentication
export Authentication__ApiKey__Enabled=true

# Set the API key
export Authentication__ApiKey__Key="your-secret-api-key-here"
```

**Docker Compose:**
```yaml
services:
  headless-ide-mcp:
    environment:
      - Authentication__ApiKey__Enabled=true
      - Authentication__ApiKey__Key=your-secret-api-key-here
```

### Using API Key Authentication

When API key authentication is enabled, clients must include the API key in the `X-API-Key` header:

**Example with curl:**
```bash
curl -X POST http://localhost:5000/ \
  -H "Content-Type: application/json" \
  -H "X-API-Key: your-secret-api-key-here" \
  -d '{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "tools/list"
  }'
```

**Example with HTTP client:**
```http
POST http://localhost:5000/
Content-Type: application/json
X-API-Key: your-secret-api-key-here

{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/list"
}
```

### Health Check Endpoint

The `/health` endpoint is **always accessible** without authentication, allowing monitoring tools to check server status without requiring an API key.

### Security Recommendations

#### Development
- API key authentication: **Disabled** (default)
- Suitable for local development on trusted machines

#### Internal Dev Cluster
- API key authentication: **Enabled**
- Use a strong, randomly generated API key
- Rotate API keys periodically (every 90 days recommended)
- Share API key securely (e.g., through secret management system)

#### Production
- API key authentication: **Enabled** (required)
- Use a cryptographically secure random API key (at least 32 characters)
- Store API key in secure secret management (e.g., Azure Key Vault, AWS Secrets Manager, Kubernetes Secrets)
- Rotate API keys regularly
- Consider additional authentication layers (OAuth2, mTLS)

### Generating Secure API Keys

**PowerShell:**
```powershell
# Generate a 32-character random API key
-join ((65..90) + (97..122) + (48..57) | Get-Random -Count 32 | % {[char]$_})
```

**Bash/Linux:**
```bash
# Generate a 32-character random API key
openssl rand -base64 32
```

**Python:**
```python
import secrets
# Generate a 32-character random API key
api_key = secrets.token_urlsafe(32)
print(api_key)
```

### Authentication Failures

Failed authentication attempts are logged with the client's IP address for security monitoring:

```
[Warning] API key authentication failed: Invalid API key. Client IP: 192.168.1.100
```

Monitor these logs for potential security issues or brute-force attempts.

---

## Concurrent Usage

### Thread Safety

The Headless IDE MCP server is **fully thread-safe** and designed to handle multiple concurrent requests.

**Key Points:**

1. **Stateless Design**: Each MCP tool call is independent and stateless
2. **No Shared Mutable State**: The CommandExecutionService doesn't maintain request-specific state between calls
3. **Process Isolation**: Each command execution spawns its own isolated process
4. **No Conflicts**: Multiple concurrent calls can execute different commands simultaneously without interfering with each other

### Concurrent Execution Model

```
Client 1 → [Request 1: dotnet --version]  ─┐
                                            ├─→ MCP Server ─→ Process 1 (isolated)
Client 2 → [Request 2: git status]       ─┤
                                            ├─→ MCP Server ─→ Process 2 (isolated)
Client 3 → [Request 3: rg "pattern"]     ─┘
                                                MCP Server ─→ Process 3 (isolated)
```

Each request:
- Gets its own correlation ID for tracing
- Spawns an isolated process
- Has independent timeout handling
- Produces separate audit logs

### Workspace Access

**File System Safety:**
- Multiple commands can **read** from the workspace simultaneously (safe)
- Multiple commands writing to the **same file** may conflict (application-level concern)
- Write operations to **different files** are safe and don't conflict

**Recommendation for Write Operations:**
- If your use case involves multiple clients writing to the workspace, implement application-level coordination
- Use unique file names per client/session to avoid conflicts
- Consider using the `/tmp` directory for temporary outputs

### Session-Based Usage

**Current Design: Stateless (No Sessions)**

The server does not maintain sessions. Each request is independent:

✅ **Advantages:**
- Simpler architecture
- No session state to manage
- Scales horizontally easily
- No session cleanup required
- Works well with load balancers

❌ **Trade-offs:**
- No command history per client
- No persistent working directory per client
- No environment variable persistence between calls

**When Sessions Might Be Needed:**

If your use case requires:
- Persistent working directory across multiple commands
- Environment variable persistence
- Command history tracking per client
- Multi-step workflows with state

Consider implementing session management at the application layer:
1. Use correlation IDs to track related requests
2. Store session state in external storage (Redis, database)
3. Pass session context in request parameters

### Example: Concurrent Usage Patterns

**Pattern 1: Multiple Independent Commands**
```javascript
// These can run concurrently without any issues
Promise.all([
  mcpClient.callTool("shell_execute", { command: "dotnet", arguments: ["--version"] }),
  mcpClient.callTool("shell_execute", { command: "git", arguments: ["status"] }),
  mcpClient.callTool("shell_execute", { command: "rg", arguments: ["TODO"] })
])
```

**Pattern 2: Sequential Workflow (if needed)**
```javascript
// Execute commands sequentially if order matters
const version = await mcpClient.callTool("shell_execute", { 
  command: "dotnet", 
  arguments: ["--version"] 
});

const build = await mcpClient.callTool("shell_execute", { 
  command: "dotnet", 
  arguments: ["build"] 
});

const test = await mcpClient.callTool("shell_execute", { 
  command: "dotnet", 
  arguments: ["test"] 
});
```

**Pattern 3: Correlation ID Tracking**
```javascript
// Use correlation IDs to track related operations
const correlationId = crypto.randomUUID();

await mcpClient.callTool("shell_execute", { 
  command: "dotnet",
  arguments: ["build"],
  correlationId: correlationId,
  user: "developer-1"
});

await mcpClient.callTool("shell_execute", { 
  command: "dotnet",
  arguments: ["test"],
  correlationId: correlationId,
  user: "developer-1"
});

// Later, search audit logs for this correlation ID to see the full workflow
```

### Resource Limits and Concurrency

The Docker resource limits apply to the **entire container**, not per request:

- **CPU**: 2 cores shared across all concurrent requests
- **Memory**: 1GB shared across all concurrent requests

**High Concurrency Recommendations:**

1. **Monitor Resource Usage**: Use `docker stats` to track container resource consumption
2. **Adjust Limits**: Increase CPU/memory limits if needed for your workload
3. **Rate Limiting**: Consider implementing rate limiting at the API gateway level
4. **Request Queuing**: For very high concurrency, implement a request queue

### Audit Logging with Concurrency

All concurrent requests are logged independently with:
- Unique correlation IDs
- Timestamps
- User identifiers
- Command details
- Execution results

This allows you to:
- Track all operations across concurrent clients
- Debug issues with specific requests
- Monitor resource usage per client/user
- Analyze usage patterns

---

## Summary

| Feature | Status | Notes |
|---------|--------|-------|
| API Key Auth | ✅ Optional | Disabled by default, enable for dev cluster/production |
| Concurrent Requests | ✅ Supported | Fully thread-safe, stateless design |
| Session Management | ❌ Not Built-in | Implement at application layer if needed |
| Workspace Conflicts | ⚠️ Possible | Application must coordinate writes to same files |
| Correlation ID Tracking | ✅ Supported | Use for tracking related operations |
| Audit Logging | ✅ Full Support | All requests logged independently |

### Quick Start Examples

**Local Development (No Auth):**
```bash
docker-compose up
# No API key needed
curl -X POST http://localhost:5000/ ...
```

**Dev Cluster (With Auth):**
```yaml
# docker-compose.yml
environment:
  - Authentication__ApiKey__Enabled=true
  - Authentication__ApiKey__Key=dev-cluster-secret-key
```

```bash
# Clients must include API key
curl -X POST http://localhost:5000/ \
  -H "X-API-Key: dev-cluster-secret-key" ...
```

---

**Last Updated**: 2024-11-15  
**Version**: Phase 2 - Production Hardening + Authentication
