# Parent Tracking Issue: Implement CLI-First Headless IDE MCP Architecture

**Status:** 📋 Ready to Create  
**Type:** Epic/Parent Issue  
**Labels:** `epic`, `enhancement`, `architecture`

---

## Overview

Implement the CLI-first Headless IDE MCP architecture as described in the design documents. This will provide AI agents with a powerful, secure, containerized development environment comparable to GitHub Actions runners.

## Background

The current MCP server has basic file system tools but lacks the ability to execute CLI commands. The new design (validated via POCs) adds:

- Shell command execution (dotnet, ripgrep, jq, etc.)
- Secure sandboxed environment
- Enhanced Docker container with CLI tools
- Optional higher-level structured tools for .NET analysis

## Design Documents

- [Design Discussion](../docs/Design-Discussion.md) - Original design proposal
- [Viability Assessment](../docs/design/poc/viability-assessment.md) - Technical feasibility analysis
- [Fail-Fast Opportunities](../docs/design/poc/fail-fast-opportunities.md) - Risk validation strategy
- [POC Code](../docs/design/poc/poc-code/README.md) - Proof of concept implementations
- [Refined Design](../docs/design/poc/refined-design.md) - Final validated design
- [Implementation Plan](../docs/design/implementation-plan.md) - Detailed breakdown

## Objectives

✅ Enable AI agents to execute arbitrary CLI commands safely  
✅ Provide secure sandboxed execution environment  
✅ Include common CLI tools (ripgrep, jq, tree, git)  
✅ Maintain strong security controls  
✅ Achieve production-ready quality  
⚠️ (Optional) Add higher-level .NET analysis tools  

## Success Metrics

### Technical
- Command execution success rate > 99%
- Container startup time < 10 seconds
- Container size < 600MB
- Build time < 5 minutes
- Integration test coverage > 80%

### Security
- Zero critical vulnerabilities in security audit
- All attack vectors mitigated
- Resource exhaustion prevented

### User Experience
- AI agents can chain commands successfully
- Clear error messages for all failures
- Comprehensive documentation

## Implementation Phases

### Phase 1: Core Shell Execution (Weeks 1-2) - CRITICAL

**Goal:** Prove CLI-first architecture with working shell_execute tool

**Sub-Issues:**
- [ ] #TBD - Issue 1.1: Add CommandExecutionService to Core
- [ ] #TBD - Issue 1.2: Add ShellTools MCP integration
- [ ] #TBD - Issue 1.3: Update Dockerfile with CLI tools
- [ ] #TBD - Issue 1.4: Add integration tests
- [ ] #TBD - Issue 1.5: Update documentation

**Deliverable:** Working shell_execute tool in containerized MCP server  
**Estimated Effort:** 46-62 hours

---

### Phase 2: Production Hardening (Weeks 2-3) - HIGH PRIORITY

**Goal:** Security and reliability for production deployment

**Sub-Issues:**
- [ ] #TBD - Issue 2.1: Add security hardening
- [ ] #TBD - Issue 2.2: Implement audit logging
- [ ] #TBD - Issue 2.3: Add resource limits
- [ ] #TBD - Issue 2.4: Security testing

**Deliverable:** Production-ready MCP server  
**Estimated Effort:** 42-58 hours

---

### Phase 3: Enhanced Tools (Weeks 3-4) - MEDIUM PRIORITY

**Goal:** Improved developer experience and reliability

**Sub-Issues:**
- [ ] #TBD - Issue 3.1: Optimize shell_execute_json
- [ ] #TBD - Issue 3.2: Enhanced error handling
- [ ] #TBD - Issue 3.3: Add monitoring and metrics

**Deliverable:** Feature-complete shell execution system  
**Estimated Effort:** 20-28 hours

---

### Phase 4: Higher-Level Tools (Weeks 4-8) - OPTIONAL

**Goal:** Structured .NET analysis tools

**Sub-Issues:**
- [ ] #TBD - Issue 4.1: Implement dotnet_project_graph
- [ ] #TBD - Issue 4.2: Implement dotnet_suggest_relevant_files
- [ ] #TBD - Issue 4.3: Documentation for structured tools

**Deliverable:** Enhanced .NET-specific capabilities  
**Estimated Effort:** 34-48 hours

---

## Total Estimated Effort

- **Phase 1-3 (Required):** 108-148 hours (~3-4 weeks)
- **Phase 4 (Optional):** 34-48 hours (~1-1.5 weeks)
- **Total:** 142-196 hours (~4.5-6.5 weeks)

## Dependencies

### External Dependencies
- ✅ .NET 8 SDK (already in use)
- ✅ Docker Desktop (already in use)
- ✅ MCP SDK (already integrated)

### Technical Prerequisites
- ✅ POC validation complete
- ✅ Design approval obtained
- ✅ Repository structure ready

## Risks and Mitigations

| Risk | Impact | Probability | Mitigation |
|------|--------|------------|------------|
| Security vulnerability found | Critical | Medium | Comprehensive security testing, regular audits |
| Performance issues | Medium | Low | Load testing, profiling, optimization |
| MCP SDK breaking changes | High | Low | Version pinning, upgrade testing |
| Container size bloat | Low | Low | Regular optimization, layer caching |

## Testing Strategy

### Unit Tests
- CommandExecutionService with all scenarios
- Path validation logic
- Timeout enforcement
- Error handling

### Integration Tests
- MCP tool calls end-to-end
- Real CLI tools in container
- Security controls validation
- Concurrent execution

### Security Tests
- Penetration testing
- Command injection attempts
- Path traversal attempts
- Resource exhaustion tests

## Deployment Strategy

1. **Phase 1 Complete:** Deploy to staging environment
2. **Phase 2 Complete:** Security audit, then production deployment
3. **Phase 3 Complete:** Production updates, monitoring
4. **Phase 4 Complete:** Optional enhancements, user feedback

## Acceptance Criteria

### Phase 1 (MVP)
- [ ] Can execute shell commands via MCP
- [ ] CLI tools available (dotnet, rg, jq, tree, git)
- [ ] Timeout enforcement working
- [ ] Path validation prevents escapes
- [ ] Integration tests passing (>80% coverage)
- [ ] Documentation updated

### Phase 2 (Production)
- [ ] Security hardening complete
- [ ] Audit logging implemented
- [ ] Resource limits configured
- [ ] Security audit passed (no critical/high vulnerabilities)
- [ ] Deployed to staging

### Phase 3 (Enhanced)
- [ ] JSON parsing optimized
- [ ] Error messages improved
- [ ] Monitoring/metrics in place
- [ ] Production-stable

### Phase 4 (Advanced) [OPTIONAL]
- [ ] At least one structured tool working
- [ ] Project graph extraction functional
- [ ] File suggestion working
- [ ] Documentation complete

## Review Process

Each sub-issue will:
1. Be implemented as a separate PR
2. Require code review approval
3. Pass all tests (unit + integration)
4. Update relevant documentation
5. Be merged into main branch

## Timeline

```
Week 1-2:   Phase 1 (Core Shell Execution)
Week 2-3:   Phase 2 (Production Hardening)
Week 3-4:   Phase 3 (Enhanced Tools)
Week 4-8:   Phase 4 (Higher-Level Tools) [OPTIONAL]
```

## Communication

- **Updates:** Weekly progress updates in this issue
- **Blockers:** Commented immediately
- **Questions:** Tag @dazinator for clarification
- **Decisions:** Documented in issue comments

## Definition of Done

✅ All Phase 1-3 issues complete  
✅ All tests passing  
✅ Documentation updated  
✅ Security audit passed  
✅ Deployed to production  
✅ User feedback positive  

## Notes

- Phase 4 can be implemented later based on user demand
- Security is non-negotiable - Phase 2 must complete before production
- Performance optimization happens throughout, not just in Phase 3
- POC code can be used as reference but should be adapted to fit repository patterns

## Related Issues

- Original design discussion: #TBD
- Viability assessment: N/A (in docs)
- Security considerations: N/A (in docs)

---

## How to Use This Issue

1. **Create Sub-Issues:** Use the implementation plan to create detailed sub-issues
2. **Link Issues:** Link all sub-issues to this parent issue
3. **Track Progress:** Update checkboxes as sub-issues are completed
4. **Weekly Updates:** Add comment with progress summary each week
5. **Blockers:** Comment immediately if blocked
6. **Close:** Close this issue when all phases are complete

---

**Created:** 2025-11-14  
**Target Completion:** TBD based on team capacity  
**Priority:** High (Core capability)  
**Complexity:** High (Multiple phases, security critical)
