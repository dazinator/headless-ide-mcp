# DevBuddy Documentation

Welcome to the Headless IDE Model Context Protocol (MCP) server documentation.

## Quick Links

- **[Getting Started](getting-started.md)** - Learn how to run and use the MCP server
- **[Claude Desktop Setup](claude-desktop-setup.md)** - Connect Claude Desktop to the containerized MCP server
- **[Project Setup](project-setup.md)** - Understand the architecture and how to add new tools
- **[Authentication](authentication.md)** - API key authentication and concurrent usage
- **[Security](security.md)** - Security architecture and controls
- **[Operations](operations.md)** - Monitoring, logging, and maintenance procedures
- **[Shell Execution Usage](shell-execution-usage.md)** - Guide to using shell execution tools

## What is DevBuddy?

The DevBuddy server is an ASP.NET Core application that provides Model Context Protocol (MCP) tools for analyzing .NET codebases and executing shell commands in a secure, sandboxed environment. This server exposes MCP tools that can be consumed by AI assistants like Claude Desktop and other MCP clients to understand and work with .NET projects.

## Key Features

- **MCP Server**: ASP.NET Core application using the official [ModelContextProtocol.AspNetCore](https://www.nuget.org/packages/ModelContextProtocol.AspNetCore) SDK
- **Shell Command Execution**: Execute CLI commands (dotnet, git, ripgrep, jq, etc.) in a sandboxed environment
- **File System Tools**: Check file existence and analyze project structure
- **Docker Support**: Full containerization with DevContainer base image including development tools
- **Production Security**: Command allowlist/denylist, error sanitization, and comprehensive audit logging
- **Resource Limits**: Docker CPU and memory limits to prevent resource exhaustion
- **Container Security**: Non-root user, capability dropping, and no-new-privileges mode

## Getting Started

1. **[Set up the server](getting-started.md)** - Install and run the MCP server locally or in Docker
2. **[Connect Claude Desktop](claude-desktop-setup.md)** - Configure Claude Desktop to use the MCP server
3. **[Explore the tools](getting-started.md#available-mcp-tools)** - Learn about available MCP tools
4. **[Secure your deployment](security.md)** - Review security features and best practices

## Available MCP Tools

### File System Tools
- `check_file_exists`: Check if a file exists in the codebase

### Shell Execution Tools
- `shell_execute`: Execute CLI commands (dotnet, git, rg, jq, etc.)
- `shell_execute_json`: Execute commands that return JSON output
- `shell_get_available_tools`: List available CLI tools in the container

## Contributing

Contributions are welcome! Please ensure:
- All tests pass (`dotnet test`)
- Integration tests cover new functionality
- Documentation is updated
- Docker build succeeds

## License

See [LICENCE.md](../LICENCE.md) for details.
