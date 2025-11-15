# Implementation Plan: CLI-First Headless IDE MCP

**Date:** 2025-11-15 (Updated)  
**Version:** 2.0 (Simplified with DevContainer)  
**Status:** Ready for Breakdown into Issues  
**Author:** Copilot Agent

---

## Overview

This document breaks down the implementation of the simplified CLI-first Headless IDE MCP architecture into concrete, actionable sub-issues. Each sub-issue represents a complete unit of work that can be implemented as a separate PR.

**Simplifications in v2.0:**
- Using DevContainer base image (reduces configuration effort)
- Focus on shell execution only (no OmniSharp, no higher-level .NET tools)
- Deferred features moved to separate future work items

---

## Implementation Phases

```
Phase 1: Core Shell Execution (1-2 weeks)
  ├── Issue 1.1: Add CommandExecutionService to Core
  ├── Issue 1.2: Add ShellTools MCP integration
  ├── Issue 1.3: Update Dockerfile to DevContainer base
  ├── Issue 1.4: Add integration tests
  └── Issue 1.5: Update documentation

Phase 2: Production Hardening (1 week)
  ├── Issue 2.1: Add security hardening
  ├── Issue 2.2: Implement audit logging
  ├── Issue 2.3: Add resource limits
  └── Issue 2.4: Security testing

Phase 3: Enhanced Tools (1 week)
  ├── Issue 3.1: Optimize shell_execute_json
  ├── Issue 3.2: Enhanced error handling
  └── Issue 3.3: Add monitoring and metrics

DEFERRED (separate future work items):
  - Higher-level .NET tools (dotnet_project_graph, etc.)
  - LSP/OmniSharp integration
  - Advanced file operations
```

---

## Phase 1: Core Shell Execution

### Issue 1.1: Add CommandExecutionService to Core

**Title:** Implement process execution service with security controls

**Description:**
Add the `CommandExecutionService` class to `HeadlessIdeMcp.Core` to enable secure command execution in sandboxed environment.

**Acceptance Criteria:**
- [ ] `ICommandExecutionService` interface defined
- [ ] `CommandExecutionService` class implemented
- [ ] `ExecutionRequest` and `ExecutionResult` models created
- [ ] `CommandExecutionOptions` configuration class added
- [ ] Timeout enforcement working (kills process tree)
- [ ] Stdout/stderr captured correctly
- [ ] Path validation prevents directory traversal
- [ ] Working directory support implemented
- [ ] Environment variable support implemented
- [ ] No shell execution (direct process spawn)
- [ ] Unit tests added with >80% coverage
- [ ] All tests passing

**Files to Create/Modify:**
- `src/HeadlessIdeMcp.Core/ProcessExecution/ICommandExecutionService.cs` (NEW)
- `src/HeadlessIdeMcp.Core/ProcessExecution/CommandExecutionService.cs` (NEW)
- `src/HeadlessIdeMcp.Core/ProcessExecution/ExecutionRequest.cs` (NEW)
- `src/HeadlessIdeMcp.Core/ProcessExecution/ExecutionResult.cs` (NEW)
- `src/HeadlessIdeMcp.Core/ProcessExecution/CommandExecutionOptions.cs` (NEW)

**Implementation Guide:**
- Use POC code from `docs/design/poc/poc-code/CommandExecutionService.cs`
- Ensure async/await throughout
- Add XML documentation comments
- Follow existing code style in repository

**Testing Requirements:**
- Unit tests for all execution scenarios
- Test timeout enforcement
- Test path validation
- Test error handling
- Test concurrent execution

**Estimated Effort:** 8-12 hours

**Dependencies:** None

**Priority:** Critical (blocks all other work)

---

### Issue 1.2: Add ShellTools MCP Integration

**Title:** Implement shell_execute, shell_execute_json, and shell_get_available_tools MCP tools

**Description:**
Add MCP tool implementations that expose the CommandExecutionService to AI agents via the MCP protocol.

**Acceptance Criteria:**
- [ ] `ShellTools` class created with `[McpServerToolType]` attribute
- [ ] `shell_execute` tool implemented
- [ ] `shell_execute_json` tool implemented with JSON parsing
- [ ] `shell_get_available_tools` tool implemented
- [ ] Response models created (ShellExecuteResponse, etc.)
- [ ] Service registered in Program.cs
- [ ] All tools discoverable via MCP tools/list
- [ ] XML documentation on all public methods
- [ ] Integration tests for each tool
- [ ] All tests passing

**Files to Create/Modify:**
- `src/HeadlessIdeMcp.Server/Tools/ShellTools.cs` (NEW)
- `src/HeadlessIdeMcp.Server/Program.cs` (MODIFY)
- `src/HeadlessIdeMcp.IntegrationTests/ShellToolsTests.cs` (NEW)

**Implementation Guide:**
- Use POC code from `docs/design/poc/poc-code/ShellTools.cs`
- Follow pattern from existing `FileSystemTools.cs`
- Use descriptive `[Description]` attributes for AI agents
- Handle all exceptions gracefully

**Testing Requirements:**
- Integration test for shell_execute with simple command
- Integration test for shell_execute_json with jq
- Integration test for shell_get_available_tools
- Test error scenarios (invalid command, timeout, etc.)

**Estimated Effort:** 8-12 hours

**Dependencies:** Issue 1.1

**Priority:** Critical

---

### Issue 1.3: Update Dockerfile to DevContainer Base

**Title:** Switch to DevContainer base image and configure for MCP server

**Description:**
Update the Dockerfile to use the Microsoft DevContainer base image (mcr.microsoft.com/devcontainers/dotnet:1-8.0) which includes pre-configured user, tools, and environment. This simplifies configuration and aligns with GitHub Codespaces/Copilot agents.

**Acceptance Criteria:**
- [ ] Dockerfile updated to use DevContainer base image
- [ ] Ripgrep installed (only additional tool needed)
- [ ] Tool verification during build
- [ ] Working /workspace directory created with correct ownership
- [ ] vscode user from DevContainer used (no manual user creation needed)
- [ ] Container builds successfully
- [ ] All tools accessible from within container (dotnet, git, curl, wget, jq, tree, nano, bash, rg)
- [ ] docker-compose.yml updated (no user override needed)
- [ ] Health check configured

**Files to Create/Modify:**
- `Dockerfile` (MODIFY - switch to DevContainer base)
- `docker-compose.yml` (MODIFY - remove user override, DevContainer handles it)
- `.dockerignore` (NEW or UPDATE)

**DevContainer Base Benefits:**
- Non-root user (vscode) pre-configured
- Git, curl, wget, jq, tree, nano, bash pre-installed
- Rich PATH including developer tools
- Consistent with Codespaces/Copilot agents
- No manual user/permission configuration

**Tools Pre-installed in DevContainer:**
- ✅ dotnet 8.0 SDK
- ✅ git
- ✅ curl, wget
- ✅ jq
- ✅ tree  
- ✅ nano
- ✅ bash (with oh-my-zsh)
- ➕ ripgrep (install via apt-get)

**Implementation Guide:**
- Use `docs/design/poc/poc-code/Dockerfile.enhanced` as reference
- Multi-stage build: DevContainer base → build → final with DevContainer
- Only install ripgrep via apt-get
- Verify all tools during build
- Use vscode user from DevContainer

**Testing Requirements:**
- Build container successfully
- Verify all tools available: `docker exec <container> rg --version`
- Test with sample commands as vscode user
- Measure container size: `docker images` (expect ~2GB)
- Measure build time

**Trade-offs:**
- Larger image (~2GB vs ~490MB) but comprehensive developer environment
- Reduced configuration complexity
- Industry-standard base

**Estimated Effort:** 3-4 hours (reduced from 4-6 due to less configuration)

**Dependencies:** None (can be parallel with 1.1, 1.2)

**Priority:** Critical

---

### Issue 1.4: Add Integration Tests

**Title:** Add comprehensive integration tests for shell execution in container

**Description:**
Create integration tests that validate shell execution works correctly in the actual Docker container environment.

**Acceptance Criteria:**
- [ ] Integration tests execute against running container
- [ ] Tests cover all MCP shell tools
- [ ] Tests validate CLI tools work (dotnet, rg, jq, tree)
- [ ] Tests verify timeout enforcement
- [ ] Tests verify path validation
- [ ] Tests verify concurrent execution
- [ ] Tests run in CI/CD pipeline
- [ ] Test coverage > 80%
- [ ] All tests passing
- [ ] CI/CD integration configured

**Files to Create/Modify:**
- `src/HeadlessIdeMcp.IntegrationTests/ShellExecutionIntegrationTests.cs` (NEW)
- `docker-compose.test.yml` (NEW)
- `.github/workflows/ci.yml` (MODIFY)

**Test Scenarios:**
1. Execute simple command (echo)
2. Execute dotnet --version
3. Search with ripgrep
4. Parse JSON with jq
5. List directory with tree
6. Verify timeout kills process
7. Verify path validation blocks /etc access
8. Execute 10 concurrent commands
9. Test with large output (500+ lines)
10. Test error scenarios

**Implementation Guide:**
- Use `WebApplicationFactory` for integration testing
- Ensure cleanup after each test
- Use realistic sample codebase for testing
- Mock or stub external dependencies if needed

**Testing Requirements:**
- All integration tests must pass locally
- All integration tests must pass in CI/CD
- Tests must be idempotent (repeatable)
- Tests must clean up resources

**Estimated Effort:** 12-16 hours

**Dependencies:** Issues 1.1, 1.2, 1.3

**Priority:** High

---

### Issue 1.5: Update Documentation

**Title:** Update documentation with shell execution tools and usage guide

**Description:**
Update all documentation to reflect the new CLI-first architecture and shell execution capabilities.

**Acceptance Criteria:**
- [ ] README.md updated with shell_execute examples
- [ ] docs/getting-started.md updated
- [ ] docs/project-setup.md updated
- [ ] .http/test-mcp-server.http updated with shell_execute examples
- [ ] New docs/usage-guide.md created
- [ ] Security section added to docs
- [ ] All code examples tested and working
- [ ] Screenshots/diagrams updated if applicable

**Files to Create/Modify:**
- `README.md` (MODIFY)
- `docs/getting-started.md` (MODIFY)
- `docs/project-setup.md` (MODIFY)
- `docs/usage-guide.md` (NEW)
- `docs/security.md` (NEW)
- `.http/test-mcp-server.http` (MODIFY)

**Documentation Sections:**
1. **README.md:**
   - Add shell_execute to features list
   - Update quick start with shell examples
   - Update tool list

2. **docs/usage-guide.md:**
   - How to use shell_execute
   - Common CLI patterns (rg, jq, dotnet)
   - Chaining commands
   - Error handling
   - Best practices

3. **docs/security.md:**
   - Security model overview
   - Container isolation
   - Path validation
   - Timeout enforcement
   - Production deployment recommendations

4. **.http/test-mcp-server.http:**
   - Add shell_execute examples
   - Add shell_execute_json examples
   - Add shell_get_available_tools example

**Implementation Guide:**
- Use clear, concise language
- Include practical examples
- Add troubleshooting section
- Link to design documents

**Testing Requirements:**
- All code examples must be tested
- Links must work
- Formatting must be correct (Markdown)
- Spell check

**Estimated Effort:** 6-8 hours

**Dependencies:** Issues 1.1, 1.2, 1.3

**Priority:** Medium

---

## Phase 2: Production Hardening

### Issue 2.1: Add Security Hardening

**Title:** Implement production security controls (error sanitization, resource limits, command controls)

**Description:**
Add production-grade security controls to prevent information disclosure and resource abuse.

**Acceptance Criteria:**
- [ ] Error message sanitization implemented
- [ ] No filesystem paths in error messages
- [ ] Generic error messages for security violations
- [ ] Command allowlist/denylist configurable
- [ ] Configuration loaded from appsettings.json
- [ ] Docker security options added (no-new-privileges, cap_drop)
- [ ] Read-only root filesystem in container
- [ ] All security tests passing
- [ ] Security documentation updated

**Files to Create/Modify:**
- `src/HeadlessIdeMcp.Core/ProcessExecution/CommandExecutionService.cs` (MODIFY)
- `src/HeadlessIdeMcp.Server/appsettings.json` (MODIFY)
- `docker-compose.yml` (MODIFY)
- `docs/security.md` (MODIFY)

**Security Controls:**
1. Error message sanitization
2. Command allowlist (optional)
3. Command denylist (rm, dd, mkfs, etc.)
4. Docker security options
5. Read-only filesystem

**Implementation Guide:**
- Wrap all exceptions with sanitized messages
- Log full errors server-side only
- Add configuration validation on startup
- Test with malicious inputs

**Testing Requirements:**
- Security penetration tests
- Test error message sanitization
- Test command denylist
- Test Docker security options

**Estimated Effort:** 8-12 hours

**Dependencies:** Phase 1 complete

**Priority:** High

---

### Issue 2.2: Implement Audit Logging

**Title:** Add audit logging for all command executions

**Description:**
Implement comprehensive audit logging to track all command executions for security monitoring and debugging.

**Acceptance Criteria:**
- [ ] All command executions logged
- [ ] Log includes: timestamp, command, args, user, exit code, duration
- [ ] Logs written to structured format (JSON)
- [ ] Log level configurable (Debug, Info, Warning, Error)
- [ ] Logs include correlation ID for tracing
- [ ] Sensitive data redacted from logs
- [ ] Log retention policy documented
- [ ] Logs queryable for security analysis
- [ ] Documentation updated

**Files to Create/Modify:**
- `src/HeadlessIdeMcp.Core/ProcessExecution/CommandExecutionService.cs` (MODIFY)
- `src/HeadlessIdeMcp.Server/appsettings.json` (MODIFY)
- `docs/operations.md` (NEW)

**Log Format:**
```json
{
  "timestamp": "2025-11-14T23:00:00Z",
  "correlationId": "abc-123",
  "command": "dotnet",
  "arguments": ["--version"],
  "workingDirectory": "/workspace",
  "exitCode": 0,
  "executionTimeMs": 123,
  "timedOut": false,
  "userId": "mcp-user"
}
```

**Implementation Guide:**
- Use ILogger<T> for logging
- Add correlation ID middleware
- Use structured logging (not string interpolation)
- Consider performance impact

**Testing Requirements:**
- Verify logs written for all executions
- Test log format is valid JSON
- Test sensitive data redaction
- Performance test (logging overhead < 5ms)

**Estimated Effort:** 6-8 hours

**Dependencies:** Issue 2.1

**Priority:** Medium

---

### Issue 2.3: Add Resource Limits

**Title:** Implement Docker resource limits (CPU, memory, process count)

**Description:**
Add Docker resource limits to prevent resource exhaustion attacks and ensure fair resource allocation.

**Acceptance Criteria:**
- [ ] CPU limits configured (2 cores max)
- [ ] Memory limits configured (1GB max)
- [ ] Memory reservations set (512MB)
- [ ] Process count limits added (if possible)
- [ ] Limits tested under load
- [ ] Container restarts gracefully when OOM
- [ ] Metrics collected for resource usage
- [ ] Documentation updated

**Files to Create/Modify:**
- `docker-compose.yml` (MODIFY)
- `docs/operations.md` (MODIFY)

**Resource Limits:**
```yaml
deploy:
  resources:
    limits:
      cpus: '2'
      memory: 1G
    reservations:
      cpus: '0.5'
      memory: 512M
```

**Implementation Guide:**
- Start with conservative limits
- Monitor resource usage in production
- Adjust based on real workload
- Add health checks for OOM scenarios

**Testing Requirements:**
- Load test with resource limits
- Test OOM scenarios
- Test CPU saturation
- Verify graceful degradation

**Estimated Effort:** 4-6 hours

**Dependencies:** Issue 2.1

**Priority:** Medium

---

### Issue 2.4: Security Testing

**Title:** Conduct security penetration testing and vulnerability assessment

**Description:**
Perform comprehensive security testing to identify and fix vulnerabilities before production deployment.

**Acceptance Criteria:**
- [ ] Penetration test plan created
- [ ] All attack vectors tested
- [ ] No critical vulnerabilities found
- [ ] No high-severity vulnerabilities found
- [ ] Medium vulnerabilities documented with mitigation
- [ ] Security test report created
- [ ] Recommendations implemented
- [ ] Container image scanned for CVEs
- [ ] Security sign-off obtained

**Attack Vectors to Test:**
1. Command injection attempts
2. Path traversal attacks
3. Container escape attempts
4. Resource exhaustion (CPU, memory, disk)
5. Information disclosure
6. Privilege escalation
7. Network attacks
8. DoS attacks

**Files to Create:**
- `docs/security-test-report.md` (NEW)
- `docs/security-checklist.md` (NEW)

**Implementation Guide:**
- Use automated security scanning tools
- Manual penetration testing
- Threat modeling
- Code review for security issues

**Testing Requirements:**
- Document all findings
- Prioritize by severity
- Fix or mitigate all critical/high issues
- Track all vulnerabilities

**Estimated Effort:** 16-24 hours

**Dependencies:** Issues 2.1, 2.2, 2.3

**Priority:** Critical (before production)

---

## Phase 3: Enhanced Tools

### Issue 3.1: Optimize shell_execute_json

**Title:** Enhance JSON execution and parsing with better error handling

**Description:**
Improve the shell_execute_json tool with better JSON validation, error messages, and performance.

**Acceptance Criteria:**
- [ ] Validates JSON before parsing
- [ ] Provides helpful error messages for invalid JSON
- [ ] Supports large JSON outputs (>1MB)
- [ ] Handles streaming JSON (if needed)
- [ ] Performance optimized (< 10ms overhead)
- [ ] Examples added for common use cases
- [ ] Documentation updated
- [ ] All tests passing

**Files to Create/Modify:**
- `src/HeadlessIdeMcp.Server/Tools/ShellTools.cs` (MODIFY)
- `src/HeadlessIdeMcp.IntegrationTests/ShellToolsTests.cs` (MODIFY)
- `docs/usage-guide.md` (MODIFY)

**Enhancements:**
1. Better JSON validation
2. Streaming JSON support (if large files)
3. JSON schema validation (optional)
4. Pretty-print option
5. JsonPath query support (optional)

**Testing Requirements:**
- Test with valid JSON
- Test with invalid JSON
- Test with large JSON (>1MB)
- Performance tests

**Estimated Effort:** 6-8 hours

**Dependencies:** Phase 1 complete

**Priority:** Low

---

### Issue 3.2: Enhanced Error Handling

**Title:** Improve error messages and add troubleshooting guidance

**Description:**
Enhance error handling to provide clear, actionable error messages with troubleshooting suggestions.

**Acceptance Criteria:**
- [ ] All error messages are clear and actionable
- [ ] Error messages include troubleshooting hints
- [ ] Common errors documented in FAQ
- [ ] Error codes standardized
- [ ] Correlation IDs in all errors
- [ ] Error tracking/metrics added
- [ ] Documentation updated
- [ ] All error scenarios tested

**Files to Create/Modify:**
- `src/HeadlessIdeMcp.Core/ProcessExecution/CommandExecutionService.cs` (MODIFY)
- `src/HeadlessIdeMcp.Server/Tools/ShellTools.cs` (MODIFY)
- `docs/troubleshooting.md` (NEW)
- `docs/error-codes.md` (NEW)

**Error Categories:**
1. Command not found
2. Timeout exceeded
3. Path validation failed
4. Permission denied
5. Invalid arguments
6. Process execution failed
7. JSON parsing failed

**Implementation Guide:**
- Standardize error format
- Add error codes (E001, E002, etc.)
- Include troubleshooting hints
- Link to documentation

**Testing Requirements:**
- Test all error scenarios
- Verify error messages are helpful
- Test correlation ID tracking

**Estimated Effort:** 6-8 hours

**Dependencies:** Phase 1 complete

**Priority:** Medium

---

### Issue 3.3: Add Monitoring and Metrics

**Title:** Implement metrics collection for performance and reliability monitoring

**Description:**
Add comprehensive metrics collection to monitor system health, performance, and usage patterns.

**Acceptance Criteria:**
- [ ] Metrics endpoint exposed (/metrics)
- [ ] Command execution metrics collected
- [ ] Response time metrics (p50, p95, p99)
- [ ] Error rate metrics
- [ ] Resource usage metrics
- [ ] Health check metrics
- [ ] Dashboard created (Grafana/similar)
- [ ] Alerts configured for anomalies
- [ ] Documentation updated

**Metrics to Collect:**
1. Command execution count
2. Execution duration (histogram)
3. Error rate (by error type)
4. Timeout rate
5. Concurrent executions
6. Resource usage (CPU, memory)
7. Tool usage (which tools used most)

**Files to Create/Modify:**
- `src/HeadlessIdeMcp.Server/Program.cs` (MODIFY)
- `src/HeadlessIdeMcp.Server/Middleware/MetricsMiddleware.cs` (NEW)
- `docs/operations.md` (MODIFY)

**Implementation Guide:**
- Use Prometheus format for metrics
- Add middleware for automatic tracking
- Minimal performance impact (< 1ms overhead)
- Consider sampling for high-volume scenarios

**Testing Requirements:**
- Test metrics endpoint returns valid data
- Test metrics accuracy
- Performance test (overhead < 1ms)

**Estimated Effort:** 8-12 hours

**Dependencies:** Phase 1 complete

**Priority:** Low

---

## Deferred Features (Separate Future Work Items)

The following features are **OUT OF SCOPE** for this implementation and will be addressed as separate issues/work items in the future:

### Higher-Level .NET Tools
- **dotnet_project_graph** - Structured project/solution analysis
- **dotnet_suggest_relevant_files** - AI-powered file suggestion
- **dotnet_diGraph** - DI container analysis  
- **policy_validateCodingRules** - Architecture validation

**Rationale:** AI agents can compose shell_execute calls to achieve similar results. These tools add value but aren't essential for MVP. Defer to validate shell execution approach first.

### LSP/OmniSharp Integration
- Separate container with OmniSharp
- LSP-MCP bridge
- Semantic code navigation
- Multi-container orchestration

**Rationale:** Adds significant complexity and can be implemented independently once shell execution is proven valuable.

### Additional MCP Tools
- Advanced file operations beyond check_file_exists
- Project scaffolding helpers
- Code generation tools

**Rationale:** Keep scope minimal. Add based on user feedback and validated use cases.

---

## Issue Tracking Template
- All examples must work
- Documentation reviewed
- Spell check and formatting

**Estimated Effort:** 6-8 hours

**Dependencies:** Issues 4.1, 4.2

**Priority:** Low (optional enhancement)

---

## Issue Tracking Template

For each issue, create in GitHub with this template:

```markdown
## Description
[Description from above]

## Acceptance Criteria
- [ ] [Criterion 1]
- [ ] [Criterion 2]
...

## Files to Create/Modify
- `path/to/file.cs` (NEW/MODIFY)

## Implementation Guide
[Guide from above]

## Testing Requirements
[Requirements from above]

## Estimated Effort
[Hours from above]

## Dependencies
- Blocks: #issue-number
- Blocked by: #issue-number

## Priority
[Critical/High/Medium/Low]

## Phase
Phase [1/2/3/4]

## Labels
- `enhancement`
- `phase-1` (or phase-2, phase-3, phase-4)
- `priority-critical` (or priority-high, priority-medium, priority-low)
```

---

## Summary

### Total Estimated Effort (Updated for v2.0 - Simplified Scope)

| Phase | Issues | Estimated Hours | Estimated Weeks | Notes |
|-------|--------|----------------|-----------------|-------|
| Phase 1 | 5 | 42-58 hours | 1-1.5 weeks | Reduced 4 hours (DevContainer simplifies setup) |
| Phase 2 | 4 | 42-58 hours | 1-1.5 weeks | Unchanged |
| Phase 3 | 3 | 20-28 hours | 0.5-1 week | Unchanged |
| **Total (In Scope)** | **12** | **104-144 hours** | **2.5-4 weeks** | **Reduced from 142-196 hours** |
| Deferred | - | - | - | Higher-level tools, LSP moved to future work |

**Effort Reduction:** ~38-52 hours saved by:
- Using DevContainer (saves ~4 hours in configuration)
- Removing Phase 4 higher-level tools (saves 34-48 hours)
- Simplified scope focused on shell execution only

### Critical Path

```
Issue 1.1 (CommandExecutionService)
    ↓
Issue 1.2 (ShellTools) ← Issue 1.3 (DevContainer Dockerfile)
    ↓
Issue 1.4 (Integration Tests)
    ↓
Issue 1.5 (Documentation)
    ↓
Phase 1 Complete → Deploy to Staging
    ↓
Phase 2 (Security Hardening) → Deploy to Production
    ↓
Phase 3 (Enhancements) → Production stable
    ↓
FUTURE: Higher-level tools, LSP (separate work items)
```

### Milestone Definitions

**Milestone 1: MVP (Phase 1 Complete)** ⭐ PRIMARY GOAL
- shell_execute working in DevContainer
- CLI tools available (dotnet, rg, jq, git, tree, etc.)
- Basic security controls
- Integration tests passing
- Documentation updated
- **Deliverable:** Usable MCP server for shell command execution

**Milestone 2: Production Ready (Phase 2 Complete)**
- Security hardening complete
- Audit logging implemented
- Resource limits configured
- Security testing passed
- Ready for production deployment

**Milestone 3: Enhanced (Phase 3 Complete)** 
- Optimized JSON handling
- Better error messages
- Monitoring and metrics
- Production-proven and stable
- **Deliverable:** Feature-complete, production-quality MCP server

**FUTURE: Advanced Features (Deferred)**
- Structured .NET tools (separate work items)
- LSP/OmniSharp integration (separate work items)
- Additional MCP tools as needed (based on user feedback)

---

## Next Steps

1. **Create Parent Tracking Issue:**
   - Title: "Implement CLI-First Headless IDE MCP Architecture"
   - Link to this implementation plan
   - Create milestones in GitHub

2. **Create Sub-Issues:**
   - Create all Phase 1 issues
   - Create Phase 2-4 issues as drafts
   - Link dependencies between issues

3. **Set Up Project Board:**
   - Columns: Backlog, In Progress, In Review, Done
   - Add all issues to board
   - Track progress

4. **Begin Implementation:**
   - Start with Issue 1.1
   - Follow dependency order
   - One PR per issue
   - Review and merge iteratively

---

**Document Version:** 1.0  
**Last Updated:** 2025-11-14  
**Status:** ✅ Ready for Issue Creation
