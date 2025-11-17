# Security Documentation

This document describes the security controls implemented in the Headless IDE MCP server to ensure safe production deployment.

## Table of Contents

- [Security Architecture](#security-architecture)
- [Authentication](#authentication)
- [Command Execution Security](#command-execution-security)
- [Error Message Sanitization](#error-message-sanitization)
- [Container Security](#container-security)
- [Resource Limits](#resource-limits)
- [Audit Logging](#audit-logging)
- [Security Configuration](#security-configuration)
- [Best Practices](#best-practices)

## Security Architecture

The Headless IDE MCP server implements defense-in-depth security with multiple layers of protection:

1. **Authentication**: Optional API key authentication for access control
2. **Container Isolation**: Runs in Docker with limited privileges
3. **Command Validation**: Allowlist/denylist enforcement
4. **Path Restrictions**: Confined to approved directories
5. **Error Sanitization**: Prevents information disclosure
6. **Audit Logging**: Tracks all command executions
7. **Resource Limits**: Prevents resource exhaustion attacks

## Authentication

The server supports optional API key authentication. See [Authentication Documentation](authentication.md) for details.

**Development**: Authentication disabled by default for local use
**Production**: Enable API key authentication for security

```json
{
  "Authentication": {
    "ApiKey": {
      "Enabled": true,
      "Key": "your-secure-api-key"
    }
  }
}
```

## Command Execution Security

### Command Allowlist/Denylist

Commands can be restricted using allowlist or denylist configuration:

**Denylist** (Default): Blocks known dangerous commands
```json
{
  "CommandExecution": {
    "DeniedCommands": [
      "rm", "dd", "mkfs", "fdisk", "format", "shutdown", "reboot", "init"
    ]
  }
}
```

**Allowlist** (Recommended for Production): Only permits specific commands
```json
{
  "CommandExecution": {
    "AllowedCommands": [
      "dotnet", "git", "rg", "jq", "tree", "bash", "curl", "find"
    ]
  }
}
```

If `AllowedCommands` is set (non-null), only those commands will be permitted. The denylist is checked first, then the allowlist.

### Path Restrictions

All command executions are confined to allowed paths:

```json
{
  "CommandExecution": {
    "AllowedPaths": [
      "/workspace",
      "/tmp"
    ]
  }
}
```

Attempts to access paths outside these directories will be denied with an `UnauthorizedAccessException`.

### Timeout Limits

Maximum execution time prevents runaway processes:

```json
{
  "CommandExecution": {
    "MaxTimeoutSeconds": 300
  }
}
```

Commands exceeding this limit are automatically terminated.

### Process Isolation

- **No Shell Execution**: Commands are executed directly (not through shell) to prevent command injection
- **Process Cleanup**: Child processes are terminated when timeout occurs
- **Environment Isolation**: Each command runs in isolated environment with controlled variables

## Error Message Sanitization

Error sanitization prevents information disclosure in production environments.

### Configuration

```json
{
  "CommandExecution": {
    "SanitizeErrorMessages": true
  }
}
```

**Development** (default): `SanitizeErrorMessages: false`
- Full error messages with paths and details
- Helpful for debugging

**Production** (recommended): `SanitizeErrorMessages: true`
- Generic error messages
- No filesystem paths exposed
- Exception details logged but not returned to clients

### Sanitized Error Types

| Original Exception | Sanitized Message |
|-------------------|-------------------|
| `DirectoryNotFoundException` with path | "The requested directory does not exist" |
| `UnauthorizedAccessException` with path | "Access to the requested path is not permitted" |
| Command execution failure with details | "Command execution failed. Check audit logs for details." |
| Denied command with name | "Command not permitted" |

## Container Security

### Docker Security Options

The container runs with hardened security settings:

```yaml
security_opt:
  - no-new-privileges:true  # Prevents privilege escalation
cap_drop:
  - ALL                     # Drop all capabilities
cap_add:
  - CHOWN                   # Only add required capabilities
  - SETUID
  - SETGID
  - DAC_OVERRIDE
```

### Non-Root User

The container runs as the `vscode` user (non-root) to limit damage from potential exploits:

```dockerfile
USER vscode
```

### Read-Only Workspace (Production)

For production environments, the workspace should be mounted read-only to prevent unauthorized modifications:

**Production:**
```yaml
volumes:
  - ./sample-codebase:/workspace:ro
```

**Development:**
```yaml
volumes:
  # Read-write for testing - allows agent to create/modify files
  - ./sample-codebase:/workspace
```

**Note**: The default docker-compose.yml uses read-write mode for development. Add `:ro` flag for production deployments.

### Network Isolation

The container runs in an isolated Docker network:

```yaml
networks:
  - mcp-network
```

## Resource Limits

Resource limits prevent DoS and resource exhaustion attacks:

```yaml
deploy:
  resources:
    limits:
      cpus: '2.0'        # Maximum 2 CPU cores
      memory: 1G         # Maximum 1GB RAM
    reservations:
      memory: 512M       # Reserved 512MB RAM
```

### OOM Handling

The container automatically restarts on Out-of-Memory conditions:

```yaml
restart: unless-stopped
```

## Audit Logging

All command executions are logged for security monitoring and debugging.

### Audit Log Configuration

```json
{
  "CommandExecution": {
    "EnableAuditLogging": true
  },
  "Logging": {
    "LogLevel": {
      "HeadlessIdeMcp.Core.ProcessExecution.CommandExecutionService": "Information"
    }
  }
}
```

### Audit Log Contents

Each log entry includes:
- **Timestamp**: UTC timestamp of execution
- **CorrelationId**: Unique ID for tracing related operations
- **User**: User or client identifier
- **Command**: The command executed
- **Arguments**: Command arguments (with sensitive data redacted)
- **WorkingDirectory**: Working directory (redacted if sanitization enabled)
- **ExitCode**: Command exit code
- **ExecutionTime**: Duration in milliseconds
- **Status**: Started, Completed, or Failed
- **TimedOut**: Whether the command timed out

### Sensitive Data Redaction

Audit logs automatically redact sensitive patterns:
- Passwords: `password=secret` → `password=***REDACTED***`
- Tokens: `token=abc123` → `token=***REDACTED***`
- Keys: `key=xyz789` → `key=***REDACTED***`
- Secrets: `secret=value` → `secret=***REDACTED***`
- Git credentials in URLs: `https://user:token@github.com` → `https://***REDACTED***@github.com`

**Note**: Git authentication credentials configured via environment variables (GITHUB_PAT, AZDO_PAT) are stored in the container's file system but are never logged or exposed to MCP clients. See [Git Authentication](git-authentication.md) for details.

### Log Format Example

```json
{
  "Timestamp": "2024-11-15T02:30:00.000Z",
  "Level": "Information",
  "MessageTemplate": "Command execution {Status}: {Command} {Arguments}",
  "Properties": {
    "CorrelationId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "Status": "Completed",
    "Command": "dotnet",
    "Arguments": ["--version"],
    "User": "mcp-client",
    "ExitCode": 0,
    "ExecutionTimeMs": 250.5,
    "TimedOut": false
  }
}
```

## Security Configuration

### Development Configuration

`appsettings.Development.json`:
```json
{
  "CommandExecution": {
    "SanitizeErrorMessages": false,
    "EnableAuditLogging": true,
    "AllowedCommands": null,
    "DeniedCommands": ["rm", "dd", "mkfs", "fdisk"]
  }
}
```

### Production Configuration

`appsettings.json`:
```json
{
  "CommandExecution": {
    "MaxTimeoutSeconds": 300,
    "AllowedPaths": ["/workspace", "/tmp"],
    "AllowedCommands": [
      "dotnet", "git", "rg", "jq", "tree", "bash", "curl", "find"
    ],
    "DeniedCommands": [
      "rm", "dd", "mkfs", "fdisk", "format", "shutdown", "reboot", "init"
    ],
    "SanitizeErrorMessages": true,
    "EnableAuditLogging": true
  }
}
```

## Best Practices

### For Production Deployment

1. **Enable Error Sanitization**: Always set `SanitizeErrorMessages: true`
2. **Use Command Allowlist**: Define specific allowed commands
3. **Restrict Paths**: Limit `AllowedPaths` to minimum required
4. **Enable Audit Logging**: Always keep `EnableAuditLogging: true`
5. **Monitor Logs**: Regularly review audit logs for suspicious activity
6. **Update Denylist**: Add dangerous commands as discovered
7. **Resource Limits**: Configure appropriate CPU/memory limits
8. **Regular Updates**: Keep base image and dependencies updated
9. **Scan for CVEs**: Regularly scan container images for vulnerabilities

### For Security Monitoring

1. **Log Aggregation**: Send logs to centralized logging system (e.g., ELK, Splunk)
2. **Alerting**: Set up alerts for:
   - Failed command executions
   - Unauthorized access attempts
   - Denied commands
   - Timeouts
   - Resource limit hits
3. **Log Retention**: Keep audit logs for compliance (typically 90-365 days)
4. **Regular Audits**: Review logs weekly for anomalies

### For Testing Security

See [Security Testing](security-test-report.md) for comprehensive penetration testing procedures.

## Security Incident Response

If a security issue is discovered:

1. **Immediate**: Stop the affected container
2. **Investigate**: Review audit logs for the time period
3. **Contain**: Isolate affected systems
4. **Remediate**: Apply patches or configuration changes
5. **Document**: Record findings and actions taken
6. **Review**: Update security controls to prevent recurrence

## Contact

For security issues, please report to: [security contact information]

---

**Last Updated**: 2024-11-15  
**Version**: Phase 2 - Production Hardening
