# Refined Design: CLI-First Headless IDE MCP

**Date:** 2025-11-15 (Updated)  
**Version:** 2.1 (Simplified with DevContainer base)  
**Status:** Ready for Implementation  
**Author:** Copilot Agent

---

## 1. Executive Summary

This document presents the refined design for the Headless IDE MCP server, validated through comprehensive POCs. The design provides AI agents with a powerful, secure, containerized development environment comparable to GitHub Actions runners and GitHub Codespaces.

### Key Capabilities
- ✅ Execute arbitrary CLI commands (dotnet, ripgrep, jq, etc.)
- ✅ Secure sandboxed environment using DevContainer base
- ✅ Pre-configured developer environment (same as Codespaces/Copilot agents)
- ✅ Docker-based deployment

### Design Status
- **Viability:** ✅ Validated via POCs
- **Security:** ⚠️ Validated with production hardening needed
- **Container:** ✅ DevContainer base (~2GB, includes all tools)
- **Integration:** ✅ MCP SDK compatibility confirmed

### Scope
This design focuses on **shell execution only**. Additional capabilities (OmniSharp, LSP, higher-level .NET tools) are deferred to future work items.

---

## 2. Architecture Overview

### 2.1 System Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                        AI Agent (Claude)                     │
└───────────────────────┬─────────────────────────────────────┘
                        │ MCP Protocol (HTTP/JSON-RPC)
                        ▼
┌─────────────────────────────────────────────────────────────┐
│    Headless IDE MCP Server (DevContainer - mcr.microsoft.   │
│             com/devcontainers/dotnet:1-8.0)                  │
│  ┌────────────────────────────────────────────────────────┐ │
│  │           ASP.NET Core MCP Server Layer                │ │
│  │  ┌──────────────┐                                      │ │
│  │  │ ShellTools   │  (Focus: shell execution only)       │ │
│  │  │              │                                       │ │
│  │  └──────┬───────┘                                      │ │
│  └─────────┼──────────────────────────────────────────────┘ │
│            │                                                 │
│  ┌─────────▼─────────────────────────────────────────────┐ │
│  │        HeadlessIdeMcp.Core (Business Logic)            │ │
│  │  ┌──────────────────────┐                              │ │
│  │  │ CommandExecution     │                              │ │
│  │  │ Service              │                              │ │
│  │  └──────────┬───────────┘                              │ │
│  └─────────────┼──────────────────────────────────────────┘ │
│                │                                             │
│  ┌─────────────▼───────────────────────────────────────┐   │
│  │         System Process Execution (.NET)              │   │
│  └──────────────────────────────────────────────────────┘   │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  Pre-installed CLI Tools (from DevContainer)          │  │
│  │  - dotnet 8.0 SDK                                     │  │
│  │  - git, curl, wget                                    │  │
│  │  - bash, nano                                         │  │
│  │  - jq, tree                                           │  │
│  │  + ripgrep (installed via apt)                       │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐  │
│  │          Workspace (/workspace - mounted)             │  │
│  │         ├── project files (.cs, .csproj, .sln)        │  │
│  │         └── build artifacts                           │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                              │
│  DevContainer Benefits:                                     │
│  - Non-root user (vscode) pre-configured                   │
│  - Rich PATH with developer tools                          │
│  - Consistent with Codespaces/Copilot agents               │
│  - Standard developer environment setup                    │
└─────────────────────────────────────────────────────────────┘
```

### 2.2 Component Responsibilities

#### ASP.NET Core MCP Server Layer
- Expose MCP protocol endpoints (HTTP/JSON-RPC)
- Tool discovery and registration via attributes
- Request/response serialization
- Dependency injection container

#### HeadlessIdeMcp.Core
- Business logic for shell command execution
- Process execution with security controls
- Independent of MCP protocol

#### DevContainer Base & CLI Tools
- Pre-configured developer environment (vscode user, rich PATH)
- Pre-installed CLI utilities (dotnet, git, curl, jq, tree, nano, bash)
- Standard environment consistent with Codespaces/Copilot agents
- Process execution runtime
- Mounted workspace access

---

## 3. Core MCP Tools

This design focuses exclusively on **shell execution tools**. Additional tools (LSP, OmniSharp, higher-level .NET analysis) are deferred to separate future work items.

### 3.1 Shell Execution Tools (Phase 1 - CRITICAL)

#### shell_execute

**Purpose:** Execute arbitrary CLI commands in a sandboxed environment

**Input:**
```json
{
  "command": "string",              // Command name (e.g., "dotnet", "rg")
  "arguments": ["string"],          // Array of arguments
  "workingDirectory": "string?",    // Optional working directory
  "timeoutSeconds": 30              // Timeout (default: 30, max: 300)
}
```

**Output:**
```json
{
  "stdout": "string",               // Standard output
  "stderr": "string",               // Standard error
  "exitCode": 0,                    // Exit code (0 = success)
  "timedOut": false,                // Whether command timed out
  "executionTimeMs": 123            // Execution time in ms
}
```

**Example Usage:**
```json
// Search for "IOrderService" in C# files
{
  "command": "rg",
  "arguments": ["IOrderService", "-g", "*.cs"],
  "workingDirectory": "/workspace"
}

// List projects in solution
{
  "command": "dotnet",
  "arguments": ["sln", "list"]
}

// Build project
{
  "command": "dotnet",
  "arguments": ["build", "--no-restore"]
}
```

---

#### shell_execute_json

**Purpose:** Execute commands that return JSON and automatically parse the result

**Input:** Same as `shell_execute`

**Output:**
```json
{
  "json": { /* parsed JSON object */ },
  "parseError": "string?",          // Error if JSON parsing failed
  "stderr": "string",
  "exitCode": 0,
  "timedOut": false,
  "executionTimeMs": 123
}
```

**Example Usage:**
```json
// Parse package.json
{
  "command": "jq",
  "arguments": [".", "package.json"]
}
```

---

#### shell_get_available_tools

**Purpose:** Discover what CLI tools are available in the container

**Input:** None

**Output:**
```json
{
  "tools": [
    {
      "name": "dotnet",
      "description": ".NET SDK",
      "available": true,
      "version": "8.0.100"
    },
    {
      "name": "rg",
      "description": "ripgrep - fast text search",
      "available": true,
      "version": "ripgrep 14.0.0"
    }
  ],
  "workspacePath": "/workspace"
}
```

---

### 3.2 File System Tools (Phase 1 - EXISTING)

#### check_file_exists

**Purpose:** Check if a file exists (already implemented)

**Input:**
```json
{
  "fileName": "string"              // File path (relative or absolute)
}
```

**Output:**
```json
{
  "message": "File 'path' exists" | "File 'path' does not exist"
}
```

---

## 4. Deferred Features (Future Work Items)

The following features are explicitly **OUT OF SCOPE** for this implementation and should be addressed in separate future work items:

### 4.1 LSP Integration (OmniSharp)
- Separate container running OmniSharp
- LSP-MCP bridge for semantic code navigation
- Requires additional orchestration layer

**Rationale:** Focus on core shell execution first. LSP can be added independently without blocking this work.

### 4.2 Higher-Level .NET Tools
These tools can be implemented using CLI commands, but are deferred to reduce scope:

- `dotnet_project_graph` - Parse solution/project structure
- `dotnet_suggest_relevant_files` - File suggestion based on queries
- `dotnet_diGraph` - DI container analysis
- `policy_validateCodingRules` - Architecture/coding rules validation

**Rationale:** AI agents can achieve similar results by composing shell_execute calls (e.g., `dotnet sln list`, `rg` searches). Structured tools are nice-to-have but not essential for MVP.

### 4.3 Additional MCP Tools
- Advanced file operations beyond check_file_exists
- Project scaffolding tools
- Code generation helpers

**Rationale:** Keep initial implementation minimal and focused on proven value (shell execution).

---

## 5. Container Specification (DevContainer Base)

### 5.1 Base Image

**Image:** `mcr.microsoft.com/devcontainers/dotnet:1-8.0`

**Why DevContainer:**
- ✅ Pre-configured non-root user (`vscode`)
- ✅ Rich PATH with developer tools
- ✅ Standard tools pre-installed (git, curl, wget, nano, bash)
- ✅ Consistent with GitHub Codespaces and Copilot agents
- ✅ Same environment model used by development teams
- ✅ Avoids manual user/permission configuration
- ✅ Predictable developer environment semantics

**Trade-offs:**
- Larger image size (~1.96GB vs ~490MB for dotnet/sdk)
- More comprehensive tooling out-of-the-box
- Industry-standard developer container

### 5.2 Dockerfile

```dockerfile
# Use DevContainer as base for pre-configured developer environment
FROM mcr.microsoft.com/devcontainers/dotnet:1-8.0 AS base
WORKDIR /app

# Install ripgrep (only additional tool needed beyond DevContainer defaults)
RUN apt-get update && apt-get install -y \
    ripgrep \
    && rm -rf /var/lib/apt/lists/*

# Verify all tools are available
RUN echo "=== Tool Verification ===" && \
    echo "dotnet: $(dotnet --version)" && \
    echo "git: $(git --version)" && \
    echo "curl: $(curl --version | head -1)" && \
    echo "wget: $(wget --version | head -1)" && \
    echo "rg: $(rg --version | head -1)" && \
    echo "jq: $(jq --version)" && \
    echo "tree: $(tree --version | head -1)" && \
    echo "bash: $(bash --version | head -1)" && \
    echo "nano: $(nano --version | head -1)"

# Create workspace directory
RUN mkdir -p /workspace && chown vscode:vscode /workspace

# Set environment variables
ENV CODE_BASE_PATH=/workspace
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV ASPNETCORE_ENVIRONMENT=Production

# Expose ports
EXPOSE 8080
EXPOSE 8081

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy and restore
COPY ["src/HeadlessIdeMcp.Server/HeadlessIdeMcp.Server.csproj", "HeadlessIdeMcp.Server/"]
COPY ["src/HeadlessIdeMcp.Core/HeadlessIdeMcp.Core.csproj", "HeadlessIdeMcp.Core/"]
COPY ["src/Directory.Build.props", "./"]
COPY ["src/Directory.Packages.props", "./"]
COPY ["src/global.json", "./"]

RUN dotnet restore "HeadlessIdeMcp.Server/HeadlessIdeMcp.Server.csproj"

# Copy source and build
COPY src/HeadlessIdeMcp.Server/ HeadlessIdeMcp.Server/
COPY src/HeadlessIdeMcp.Core/ HeadlessIdeMcp.Core/

RUN dotnet build "HeadlessIdeMcp.Server/HeadlessIdeMcp.Server.csproj" -c Release -o /app/build
RUN dotnet publish "HeadlessIdeMcp.Server/HeadlessIdeMcp.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage - combine DevContainer with published app
FROM base AS final
WORKDIR /app

# Copy published application
COPY --from=build --chown=vscode:vscode /app/publish .

# Switch to non-root user (vscode user from DevContainer)
USER vscode

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "HeadlessIdeMcp.Server.dll"]
```

### 5.3 Pre-installed CLI Tools (from DevContainer)

| Tool | Pre-installed | Purpose |
|------|---------------|---------|
| dotnet | ✅ 8.0 SDK | .NET development and CLI |
| git | ✅ 2.51+ | Version control |
| curl | ✅ 7.88+ | HTTP requests |
| wget | ✅ 1.21+ | File downloads |
| jq | ✅ 1.6 | JSON processing |
| tree | ✅ 2.1+ | Directory visualization |
| nano | ✅ 7.2 | Text editor |
| bash | ✅ 5.2+ | Shell scripting |
| find/grep | ✅ Standard | File search |
| **ripgrep** | ➕ Added | Fast code search (apt install) |

**Legend:**
- ✅ = Pre-installed in DevContainer base
- ➕ = Added via apt-get

### 5.4 User Configuration

**DevContainer Benefits:**
- User: `vscode` (non-root, UID 1000)
- Home: `/home/vscode`
- Shell: bash with oh-my-zsh pre-configured
- PATH: Rich developer PATH including `/home/vscode/.dotnet/tools`
- Permissions: Correctly configured for developer workflows

**No manual configuration needed** for:
- User creation
- Group assignment  
- Home directory setup
- Shell configuration
- PATH configuration
- Tool permissions

### 5.5 Environment Variables

```dockerfile
ENV CODE_BASE_PATH=/workspace
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV ASPNETCORE_ENVIRONMENT=Production
# DevContainer provides additional developer-friendly env vars
```

### 5.6 Build Metrics (Estimated)

| Metric | DevContainer Base | Previous (dotnet/sdk) |
|--------|-------------------|----------------------|
| Base Image Size | ~1.96GB | ~450MB |
| With App | ~2.0GB | ~490MB |
| First Build Time | ~3-4 min | ~2.5 min |
| Cached Build | ~30 sec | ~20 sec |
| Tools Installed | 10+ | 8 (manual) |
| Manual Config | Minimal | Moderate |

**Analysis:** Larger image size is acceptable trade-off for:
- Industry-standard developer environment
- Reduced configuration complexity
- Consistency with Codespaces/Copilot agents
- Pre-configured user/permissions/PATH

---

## 6. Security Model

### 6.1 Container Security

#### Docker Configuration
```yaml
services:
  headless-ide-mcp:
    image: headless-ide-mcp:latest
    # DevContainer uses vscode user (UID 1000) - automatically configured
    read_only: true                # Read-only root filesystem
    security_opt:
      - no-new-privileges:true     # Prevent privilege escalation
    cap_drop:
      - ALL                        # Drop all capabilities
    networks:
      - isolated_network           # Isolated bridge network
    deploy:
      resources:
        limits:
          cpus: '2'                # Max 2 CPU cores
          memory: 1G               # Max 1GB RAM
        reservations:
          cpus: '0.5'
          memory: 512M
```

#### Volume Mounts
```yaml
volumes:
  - ./codebase:/workspace:ro       # Read-only code
  - /tmp/mcp:/tmp                  # Writable temp directory
```

### 6.2 Process Execution Security

#### Security Controls

1. **No Shell Execution:**
   - Use `Process.Start()` with direct command (not via shell)
   - Prevents command injection attacks
   - ✅ Implemented in POC

2. **Path Validation:**
   - Whitelist allowed working directories
   - Prevent directory traversal (../, ../../)
   - Normalize paths before validation
   - ✅ Implemented in POC

3. **Timeout Enforcement:**
   - Mandatory timeout (default: 30s, max: 300s)
   - Kill entire process tree on timeout
   - Prevent resource exhaustion
   - ✅ Implemented in POC

4. **Command Controls (Optional):**
   - Command allowlist (if needed for production)
   - Command denylist (dangerous commands: rm, dd, mkfs)
   - ✅ Infrastructure in POC

#### Allowed Paths
```csharp
{
  "allowedPaths": [
    "/workspace",                  // Mounted codebase
    "/tmp",                        // Temporary files
    "/app"                         // Application directory (read-only)
  ]
}
```

#### Command Denylist
```csharp
{
  "deniedCommands": [
    "rm",          // File deletion
    "dd",          // Disk operations
    "mkfs",        // Filesystem creation
    "fdisk",       // Partition management
    "mount",       // Mount operations
    "sudo",        // Privilege escalation
    "su"           // User switching
  ]
}
```

### 6.3 Additional Security Measures

1. **Error Message Sanitization:**
   - Remove sensitive paths from error messages
   - Generic error messages for security violations
   - Log full details server-side only

2. **Audit Logging:**
   - Log all command executions
   - Include: timestamp, command, user, exit code
   - Retention policy (e.g., 30 days)

3. **Network Isolation:**
   - No internet access from container (optional)
   - Use isolated Docker network
   - Whitelist only required outbound connections

4. **Regular Security Audits:**
   - Penetration testing
   - Dependency scanning
   - Container image scanning

---

## 7. Integration with MCP SDK

### 7.1 Service Registration

**Program.cs:**
```csharp
var builder = WebApplication.CreateBuilder(args);

// Get configuration
var codeBasePath = Environment.GetEnvironmentVariable("CODE_BASE_PATH") ?? "/workspace";

// Register core services
builder.Services.AddSingleton<IFileSystemService>(sp => 
    new FileSystemService(codeBasePath));

builder.Services.AddSingleton<ICommandExecutionService>(sp =>
{
    var options = new CommandExecutionOptions
    {
        MaxTimeoutSeconds = 300,
        AllowedPaths = new List<string> { codeBasePath, "/tmp" },
        DeniedCommands = new List<string> { "rm", "dd", "mkfs", "fdisk" }
    };
    return new CommandExecutionService(codeBasePath, options);
});

// Configure MCP Server
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

// Map MCP endpoints
app.MapMcp();

// Health check
app.MapGet("/health", () => Results.Ok(new 
{ 
    status = "healthy", 
    codeBasePath,
    timestamp = DateTime.UtcNow
}));

app.Run();
```

### 7.2 Tool Implementation Pattern

```csharp
[McpServerToolType]
public class ShellTools
{
    private readonly ICommandExecutionService _executionService;

    public ShellTools(ICommandExecutionService executionService)
    {
        _executionService = executionService;
    }

    [McpServerTool("shell_execute")]
    [Description("Execute a CLI command")]
    public async Task<ShellExecuteResponse> ExecuteAsync(
        [Description("Command to execute")] string command,
        [Description("Command arguments")] string[]? arguments = null,
        [Description("Working directory")] string? workingDirectory = null,
        [Description("Timeout in seconds")] int timeoutSeconds = 30)
    {
        // Implementation
    }
}
```

---

## 7. Development Workflow

### 7.1 Local Development

```bash
# Clone repository
git clone https://github.com/dazinator/headless-ide-mcp.git
cd headless-ide-mcp

# Build and run with Docker Compose
docker-compose up --build

# Test the server
curl http://localhost:5000/health

# Call MCP tools
curl -X POST http://localhost:5000/ \
  -H "Content-Type: application/json" \
  -d @test-request.json
```

### 7.2 Testing Strategy

#### Unit Tests
- Test `CommandExecutionService` with various scenarios
- Test path validation logic
- Test timeout enforcement
- Test error handling

#### Integration Tests
- Test MCP tool calls end-to-end
- Test with real CLI tools in container
- Test security controls (path traversal, timeouts)
- Test concurrent execution

#### Security Tests
- Penetration testing
- Command injection attempts
- Path traversal attempts
- Resource exhaustion tests

### 7.3 CI/CD Pipeline

```yaml
# .github/workflows/ci.yml
name: CI
on: [push, pull_request]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Build
        run: dotnet build
      - name: Unit Tests
        run: dotnet test
      - name: Build Docker Image
        run: docker build -t headless-ide-mcp:test .
      - name: Integration Tests
        run: docker-compose -f docker-compose.test.yml up --abort-on-container-exit
```

---

## 8. Usage Examples

### 8.1 AI Agent Workflow

**Scenario:** Agent needs to understand a work item

```javascript
// 1. Discover available tools
POST /
{
  "method": "tools/call",
  "params": {
    "name": "shell_get_available_tools"
  }
}

// 2. List projects in solution
POST /
{
  "method": "tools/call",
  "params": {
    "name": "shell_execute",
    "arguments": {
      "command": "dotnet",
      "arguments": ["sln", "list"]
    }
  }
}

// 3. Search for a service interface
POST /
{
  "method": "tools/call",
  "params": {
    "name": "shell_execute",
    "arguments": {
      "command": "rg",
      "arguments": ["IOrderService", "-g", "*.cs", "-A", "5"]
    }
  }
}

// 4. Check if specific file exists
POST /
{
  "method": "tools/call",
  "params": {
    "name": "check_file_exists",
    "arguments": {
      "fileName": "src/Services/OrderService.cs"
    }
  }
}

// 5. Build the project
POST /
{
  "method": "tools/call",
  "params": {
    "name": "shell_execute",
    "arguments": {
      "command": "dotnet",
      "arguments": ["build", "--no-restore"],
      "timeoutSeconds": 120
    }
  }
}
```

### 8.2 Common CLI Patterns

**Search code:**
```bash
rg "pattern" -g "*.cs" -A 3 -B 3
```

**Parse JSON:**
```bash
echo '{"key": "value"}' | jq '.key'
```

**List directory:**
```bash
tree -L 2 /workspace
```

**Find files:**
```bash
find /workspace -name "*.csproj"
```

**Git operations:**
```bash
git log --oneline -10
git diff HEAD~1
```

---

## 9. Implementation Roadmap

**Note:** This design focuses on shell execution only. Higher-level .NET tools and LSP integration are deferred to separate future work items.

### Phase 1: Core Shell Execution (Weeks 1-2) ✅ CRITICAL

**Goal:** Prove CLI-first architecture with DevContainer base

**Tasks:**
- ✅ Integrate `CommandExecutionService` into Core project
- ✅ Add `ShellTools` to Server project  
- ✅ Update Dockerfile to use DevContainer base (mcr.microsoft.com/devcontainers/dotnet:1-8.0)
- ✅ Install only ripgrep (other tools pre-installed)
- ✅ Integration tests for shell execution
- ✅ Security testing (path traversal, timeouts)
- ✅ Documentation updates

**Deliverable:** Working shell_execute tool in DevContainer-based MCP server

**Success Criteria:**
- Can execute dotnet, rg, jq, git, tree commands
- Timeouts enforced correctly
- Path validation prevents escapes
- Concurrent execution works
- All integration tests pass
- vscode user (from DevContainer) works correctly

---

### Phase 2: Production Hardening (Weeks 2-3)

**Goal:** Security and reliability for production use

**Tasks:**
- Add error message sanitization
- Implement Docker resource limits (CPU, memory)
- Add audit logging for command execution
- Security penetration testing
- Load testing (concurrent requests)
- Performance optimization

**Deliverable:** Production-ready MCP server

**Success Criteria:**
- Passes security audit
- Handles 10+ concurrent requests
- Resource exhaustion prevented
- Comprehensive audit logs

---

### Phase 3: Enhanced Tools (Weeks 3-4)

**Goal:** Improved developer experience

**Tasks:**
- Optimize shell_execute_json
- Better error messages
- Enhanced health check endpoint
- Metrics and monitoring
- Performance tuning

**Deliverable:** Feature-complete shell execution system

**Success Criteria:**
- JSON parsing works reliably
- Clear, actionable error messages
- Monitoring in place
- Production-stable

---

## 10. Future Work (Out of Scope)

The following capabilities are **explicitly deferred** to separate future work items:

### Higher-Level .NET Tools
- dotnet_project_graph (CLI-based or Roslyn)
- dotnet_suggest_relevant_files
- dotnet_diGraph (DI container analysis)
- policy_validateCodingRules

**Why Deferred:** AI agents can compose shell_execute calls to achieve similar results. These tools are valuable but not essential for MVP.

### LSP Integration (OmniSharp)
- Separate container running OmniSharp
- LSP-MCP bridge for semantic navigation
- Multi-container orchestration

**Why Deferred:** Adds complexity and can be implemented independently without blocking shell execution capability.

### Additional MCP Tools
- Advanced file operations
- Project scaffolding
- Code generation helpers

**Why Deferred:** Keep initial scope minimal and focused on validated high-value use case (shell execution).

---

## 11. Success Metrics

### Technical Metrics
- ✅ Command execution success rate > 99%
- ✅ Average command latency < 100ms overhead
- ✅ Container startup time < 10 seconds
- ✅ Zero critical security vulnerabilities
- ✅ Integration test coverage > 80%

### User Experience Metrics
- ✅ AI agent can chain commands successfully
- ✅ Clear error messages for all failure modes
- ✅ Documentation clarity (user surveys)

### Operational Metrics
- ✅ Build time < 5 minutes
- ✅ Container size < 600MB
- ✅ CI/CD pipeline success rate > 95%
- ✅ Mean time to recovery < 5 minutes

---

## 12. Risk Register

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| Security vulnerability | Medium | Critical | Regular audits, penetration testing |
| Performance degradation | Low | Medium | Load testing, monitoring |
| Container size bloat | Low | Low | Regular image optimization |
| CLI tool incompatibility | Low | Medium | Integration tests, version pinning |
| Breaking MCP SDK changes | Low | High | Version pinning, release monitoring |

---

## 13. Alternatives Considered

### Alternative 1: Pure MCP Tools (No CLI)
**Rejected:** Too much custom code, less flexible

### Alternative 2: WebAssembly Sandbox
**Rejected:** Limited tool ecosystem, compilation complexity

### Alternative 3: SSH-based Execution
**Rejected:** Additional security surface, orchestration complexity

### Decision: CLI-First Architecture
**Selected:** Best balance of flexibility, security, and simplicity

---

## 14. Appendices

### Appendix A: Complete File Structure

```
headless-ide-mcp/
├── src/
│   ├── HeadlessIdeMcp.Server/
│   │   ├── Tools/
│   │   │   ├── ShellTools.cs          # NEW: Shell execution tools
│   │   │   └── FileSystemTools.cs     # EXISTING
│   │   ├── Program.cs                 # UPDATED: New service registrations
│   │   └── HeadlessIdeMcp.Server.csproj
│   ├── HeadlessIdeMcp.Core/
│   │   ├── ProcessExecution/
│   │   │   ├── ICommandExecutionService.cs   # NEW
│   │   │   ├── CommandExecutionService.cs    # NEW
│   │   │   ├── ExecutionRequest.cs           # NEW
│   │   │   ├── ExecutionResult.cs            # NEW
│   │   │   └── CommandExecutionOptions.cs    # NEW
│   │   ├── FileSystemService.cs       # EXISTING
│   │   └── IFileSystemService.cs      # EXISTING
│   ├── HeadlessIdeMcp.IntegrationTests/
│   │   ├── ShellToolsTests.cs         # NEW: Integration tests
│   │   └── FileSystemToolsTests.cs    # EXISTING
│   └── Solution.sln
├── docs/
│   ├── design/
│   │   ├── viability-assessment.md
│   │   ├── fail-fast-opportunities.md
│   │   ├── refined-design.md          # THIS FILE
│   │   ├── implementation-plan.md     # NEXT
│   │   └── poc-code/
│   │       ├── CommandExecutionService.cs
│   │       ├── CommandExecutionServiceTests.cs
│   │       ├── ShellTools.cs
│   │       ├── Dockerfile.enhanced
│   │       └── README.md
│   ├── Design-Discussion.md           # ORIGINAL
│   ├── getting-started.md             # TO UPDATE
│   └── project-setup.md               # TO UPDATE
├── Dockerfile                         # TO UPDATE (add CLI tools)
├── docker-compose.yml                 # TO UPDATE (security options)
└── README.md                          # TO UPDATE
```

### Appendix B: Dependencies

**New NuGet Packages:**
- None (using built-in .NET APIs)

**Future Optional Dependencies:**
- Microsoft.Build.Locator (for Roslyn integration)
- Microsoft.CodeAnalysis.CSharp (for Roslyn integration)

### Appendix C: Configuration

**Environment Variables:**
```bash
CODE_BASE_PATH=/workspace              # Required: Workspace path
ASPNETCORE_ENVIRONMENT=Production      # Optional: Environment
MAX_TIMEOUT_SECONDS=300                # Optional: Max command timeout
ALLOWED_PATHS=/workspace,/tmp          # Optional: Allowed working directories
```

**appsettings.json:**
```json
{
  "CommandExecution": {
    "MaxTimeoutSeconds": 300,
    "AllowedPaths": ["/workspace", "/tmp"],
    "DeniedCommands": ["rm", "dd", "mkfs", "fdisk"],
    "EnableAuditLogging": true
  }
}
```

---

## 15. Conclusion

This refined design provides a clear, validated path to implementing the CLI-first Headless IDE MCP architecture. All critical POCs have passed, and the design is ready for phased implementation.

### Next Steps
1. Create detailed implementation plan with sub-issues
2. Begin Phase 1 implementation
3. Continuous validation through integration tests
4. Iterate based on user feedback

### Approval
Ready for stakeholder review and implementation approval.

---

**Document Version:** 2.0  
**Last Updated:** 2025-11-14  
**Status:** ✅ Approved for Implementation
