# Viability Assessment: CLI-First Headless IDE MCP Architecture

**Date:** 2025-11-14  
**Status:** Initial Assessment  
**Author:** Copilot Agent

## Executive Summary

This document assesses the viability of implementing the CLI-first architecture described in `Design-Discussion.md`. The assessment concludes that the design is **highly viable** with some important considerations around security, tooling, and incremental implementation.

## 1. CLI-First Architecture Feasibility

### Assessment: ✅ **VIABLE**

**Rationale:**
- The approach aligns with proven models (GitHub Actions runners, Copilot Agents)
- .NET SDK includes robust process execution APIs (`System.Diagnostics.Process`)
- ASP.NET Core MCP SDK supports arbitrary tool signatures
- Container isolation provides natural security boundary

**Supporting Evidence:**
- Current codebase already uses dependency injection and tool registration patterns
- MCP SDK (`ModelContextProtocol.AspNetCore`) supports dynamic tool discovery
- Docker infrastructure already in place

**Considerations:**
- Process execution requires careful timeout management
- Working directory management essential for relative path operations
- Output buffering strategies needed for long-running commands
- Stream handling for stdout/stderr separation

**Recommendation:** Proceed with implementation. Start with synchronous execution, add async streaming later if needed.

---

## 2. Security and Sandboxing

### Assessment: ⚠️ **VIABLE WITH CAUTION**

**Rationale:**
- Docker provides strong isolation when configured correctly
- Rootless containers and read-only mounts available
- Network restrictions feasible via Docker networking

**Key Security Requirements:**
1. **Container Configuration:**
   - Non-root user execution ✅ (standard Docker practice)
   - Read-only workspace mount ✅ (current docker-compose uses `:ro`)
   - No host network access ✅ (bridge network isolation)
   - Limited capabilities ⚠️ (needs explicit configuration)

2. **Process Execution Sandboxing:**
   - Timeout enforcement ✅ (easily implemented)
   - Resource limits ⚠️ (requires cgroups configuration)
   - Path traversal prevention ✅ (path validation required)
   - Command injection prevention ✅ (no shell execution, direct process spawn)

3. **File System Security:**
   - Prevent access outside `/workspace` and `/tmp` ✅ (path validation)
   - No write access to sensitive locations ✅ (container permissions)

**Risks Identified:**
- **HIGH:** Arbitrary command execution could be exploited if not properly sandboxed
- **MEDIUM:** Resource exhaustion (CPU, memory, disk) without proper limits
- **MEDIUM:** Information disclosure through error messages
- **LOW:** Timing attacks via execution time

**Mitigations:**
- Implement command allowlist or denylist as configuration option
- Enforce mandatory timeouts (default 30s, max configurable)
- Validate all paths before execution
- Use process spawning (not shell) to prevent command injection
- Add resource limits via Docker and .NET process limits
- Sanitize error output

**Recommendation:** Implement with security-first mindset. Add progressive hardening options.

---

## 3. Container Tooling Requirements

### Assessment: ✅ **VIABLE**

**Tool Availability Analysis:**

| Tool | Availability | Installation | Size Impact | Risk |
|------|-------------|--------------|-------------|------|
| dotnet SDK | ✅ Base image | Built-in | ~450MB | None |
| ripgrep | ✅ apt/binary | `apt-get install ripgrep` | ~1MB | Low |
| jq | ✅ apt | `apt-get install jq` | ~1MB | Low |
| tree | ✅ apt | `apt-get install tree` | <1MB | Low |
| bash | ✅ Base image | Built-in | 0 | None |
| findutils | ✅ Base image | Built-in | 0 | None |
| coreutils | ✅ Base image | Built-in | 0 | None |
| git | ✅ apt | `apt-get install git` | ~30MB | Low |
| curl/wget | ✅ apt | `apt-get install curl wget` | ~2MB | Low |

**Container Size Analysis:**
- Current base image (`mcr.microsoft.com/dotnet/aspnet:8.0`): ~220MB
- With SDK (`mcr.microsoft.com/dotnet/sdk:8.0`): ~450MB
- Additional CLI tools: ~40MB estimated
- **Total Estimated Size:** ~490MB (acceptable for a development container)

**Build Time Impact:**
- Initial build: ~2-3 minutes (with tool installation)
- Subsequent builds: <30 seconds (with layer caching)
- **Assessment:** Acceptable for development workflow

**Recommendation:** Use multi-stage Dockerfile with SDK for runtime (not just aspnet) to support compilation and analysis tools.

---

## 4. MCP Tool Integration Complexity

### Assessment: ✅ **LOW COMPLEXITY**

**Implementation Effort:**

1. **shell.execute Tool:**
   - Complexity: Low
   - Estimated effort: 4-8 hours
   - Dependencies: Process execution service
   - Risk: Low (well-understood pattern)

2. **shell.executeJson Tool:**
   - Complexity: Low
   - Estimated effort: 2-4 hours
   - Dependencies: shell.execute + JSON parsing
   - Risk: Low (extends shell.execute)

3. **Higher-Level Tools (dotnet.projectGraph, etc.):**
   - Complexity: Medium-High
   - Estimated effort: 16-40 hours each
   - Dependencies: MSBuild API, Roslyn, custom parsers
   - Risk: Medium (requires domain expertise)

**Current Architecture Support:**
- ✅ Dependency injection ready
- ✅ Tool auto-discovery via attributes
- ✅ Clear separation of concerns (Core vs Server)
- ✅ Integration test infrastructure exists

**Recommendation:** Incremental implementation starting with shell.execute, validate with users before building higher-level tools.

---

## 5. Technical Risks and Dependencies

### High Priority Risks

#### Risk 1: Process Execution Reliability
- **Impact:** High
- **Probability:** Medium
- **Mitigation:** Comprehensive error handling, timeout enforcement, process cleanup
- **Fail-Fast Test:** POC with various command types (fast, slow, failing, hanging)

#### Risk 2: Security Vulnerabilities
- **Impact:** Critical
- **Probability:** Medium
- **Mitigation:** Security-first implementation, regular audits, principle of least privilege
- **Fail-Fast Test:** Penetration testing POC with malicious inputs

#### Risk 3: Performance Under Load
- **Impact:** Medium
- **Probability:** Low
- **Mitigation:** Process pooling, resource limits, queue management
- **Fail-Fast Test:** Load test with concurrent command execution

### Medium Priority Risks

#### Risk 4: Tool Compatibility
- **Impact:** Medium
- **Probability:** Low
- **Mitigation:** Test with common CLI tools, document known limitations
- **Fail-Fast Test:** POC with ripgrep, jq, dotnet CLI commands

#### Risk 5: Path Handling Complexity
- **Impact:** Medium
- **Probability:** Medium
- **Mitigation:** Robust path normalization, validation utilities
- **Fail-Fast Test:** POC with various path formats (relative, absolute, with spaces, symlinks)

### Low Priority Risks

#### Risk 6: Output Streaming Complexity
- **Impact:** Low
- **Probability:** Low
- **Mitigation:** Start with buffered output, add streaming if needed
- **Fail-Fast Test:** Defer to later phase

---

## 6. Dependencies and Prerequisites

### External Dependencies
- ✅ .NET 8 SDK (already in use)
- ✅ Docker Desktop (already in use)
- ✅ MCP SDK (already integrated)

### New Dependencies Needed
- ⚠️ MSBuild API (for dotnet.projectGraph)
- ⚠️ Roslyn APIs (for dotnet.diGraph)
- ⚠️ CLI tools in container (ripgrep, jq, etc.)

### Development Environment
- ✅ Working build pipeline
- ✅ Integration test framework
- ✅ Docker Compose for local development
- ⚠️ Security testing tools (need to add)

---

## 7. Incremental Implementation Strategy

### Phase 1: Core Shell Execution (Week 1-2)
**Goal:** Prove shell.execute viability
- Implement basic process execution service
- Add shell.execute MCP tool
- Container with CLI tools installed
- Integration tests for command execution
- **Success Criteria:** Can execute dotnet, rg, jq commands successfully

### Phase 2: Security Hardening (Week 2-3)
**Goal:** Validate security model
- Add timeout enforcement
- Path validation and sandboxing
- Resource limits via Docker
- Security testing
- **Success Criteria:** Passes security audit, resource exhaustion tests

### Phase 3: Enhanced Execution (Week 3-4)
**Goal:** Production-ready execution
- shell.executeJson implementation
- Improved error handling
- Working directory management
- Environment variable support
- **Success Criteria:** Robust error handling, user-friendly outputs

### Phase 4: Higher-Level Tools (Week 4-8)
**Goal:** Value-add structured tools
- dotnet.projectGraph
- dotnet.diGraph (if time permits)
- Additional analysis tools as needed
- **Success Criteria:** At least one structured tool working end-to-end

---

## 8. Success Metrics

### Technical Metrics
- ✅ Command execution success rate > 99%
- ✅ Average command latency < 100ms overhead
- ✅ Container startup time < 10 seconds
- ✅ Zero security vulnerabilities in penetration testing

### User Experience Metrics
- ✅ AI agent can successfully chain commands
- ✅ Clear error messages for failures
- ✅ Documentation clarity (user feedback)

### Development Metrics
- ✅ Integration test coverage > 80%
- ✅ Build time < 5 minutes
- ✅ CI/CD pipeline success rate > 95%

---

## 9. Conclusion

### Overall Assessment: ✅ **HIGHLY VIABLE**

The CLI-first architecture described in Design-Discussion.md is technically sound and implementable with the current technology stack. The design aligns with industry best practices (GitHub Actions model) and leverages existing infrastructure.

### Key Strengths
1. Proven architectural pattern
2. Strong alignment with existing codebase
3. Clear separation of concerns
4. Extensible and modular design
5. Natural security boundaries via containers

### Key Challenges
1. Security requires careful implementation
2. Higher-level tools need significant effort
3. Performance testing needed under load

### Recommendation
**PROCEED with implementation** following the incremental strategy outlined above. Start with POCs to validate critical assumptions, then move to production implementation.

### Next Steps
1. Create POCs for fail-fast validation (see fail-fast-opportunities.md)
2. Implement Phase 1 (Core Shell Execution)
3. Iterate based on learnings
4. Expand to higher-level tools as needed

---

## Appendix A: Technology Stack Verification

| Technology | Current Version | Required Version | Status |
|------------|----------------|------------------|---------|
| .NET | 8.0 | 8.0+ | ✅ |
| Docker | - | 20.10+ | ✅ |
| MCP SDK | Latest | Latest | ✅ |
| ASP.NET Core | 8.0 | 8.0+ | ✅ |

---

## Appendix B: Alternative Approaches Considered

### Alternative 1: Pure MCP Tools (No CLI)
**Rejected Reason:** Requires implementing dozens of custom tools, high maintenance burden

### Alternative 2: WebAssembly Sandbox
**Rejected Reason:** Limited tool ecosystem, complexity in compilation

### Alternative 3: SSH-based Remote Execution
**Rejected Reason:** Additional security surface, complexity in orchestration

The CLI-first approach via Docker containers provides the best balance of flexibility, security, and development efficiency.
