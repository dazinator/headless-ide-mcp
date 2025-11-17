# Shell Execution Usage Guide

This guide explains how to use the shell execution tools provided by the Headless IDE MCP server.

## Overview

The MCP server provides three main shell execution tools:

1. **shell_execute** - Execute any CLI command and get stdout, stderr, and exit code
2. **shell_execute_json** - Execute commands that return JSON and automatically parse the output
3. **shell_get_available_tools** - Check which CLI tools are available in the container

> **Note:** The C# methods are named `ShellExecuteAsync`, `ShellExecuteJsonAsync`, and `ShellGetAvailableToolsAsync`, but the MCP framework automatically converts these to snake_case for the tool names.

## Available CLI Tools

The DevContainer-based Docker image includes the following tools:

- **dotnet** - .NET SDK 8.0
- **git** - Version control
- **rg** (ripgrep) - Fast text search
- **jq** - JSON processor
- **tree** - Directory visualization
- **bash** - Shell
- **curl** - Data transfer tool
- **find** - File search utility
- **nano** - Text editor
- **wget** - File downloader

## Common Usage Patterns

### .NET Development

#### Get .NET version
```json
{
  "name": "shell_execute",
  "arguments": {
    "command": "dotnet",
    "arguments": ["--version"]
  }
}
```

#### Build a project
```json
{
  "name": "shell_execute",
  "arguments": {
    "command": "dotnet",
    "arguments": ["build", "MyProject.csproj"],
    "workingDirectory": "src/MyProject",
    "timeoutSeconds": 120
  }
}
```

#### Run tests
```json
{
  "name": "shell_execute",
  "arguments": {
    "command": "dotnet",
    "arguments": ["test", "--verbosity", "normal"],
    "timeoutSeconds": 180
  }
}
```

### Code Search with Ripgrep

#### Find all TODO comments
```json
{
  "name": "shell_execute",
  "arguments": {
    "command": "rg",
    "arguments": ["TODO", "--type", "cs"]
  }
}
```

#### Search for a specific method
```json
{
  "name": "shell_execute",
  "arguments": {
    "command": "rg",
    "arguments": ["public class Calculator", "--files-with-matches"]
  }
}
```

#### Count lines of code
```json
{
  "name": "shell_execute",
  "arguments": {
    "command": "rg",
    "arguments": ["--count-matches", "^", "--type", "cs"]
  }
}
```

### JSON Processing with jq

#### Parse and extract JSON fields
```json
{
  "name": "shell_execute_json",
  "arguments": {
    "command": "jq",
    "arguments": [".version", "package.json"]
  }
}
```

#### Filter JSON array
```json
{
  "name": "shell_execute_json",
  "arguments": {
    "command": "jq",
    "arguments": [".[].name", "data.json"]
  }
}
```

### Directory Exploration

#### List directory structure
```json
{
  "name": "shell_execute",
  "arguments": {
    "command": "tree",
    "arguments": ["-L", "2", "-I", "bin|obj"]
  }
}
```

#### Find all .csproj files
```json
{
  "name": "shell_execute",
  "arguments": {
    "command": "find",
    "arguments": [".", "-name", "*.csproj"]
  }
}
```

### Git Operations

#### Check git status
```json
{
  "name": "shell_execute",
  "arguments": {
    "command": "git",
    "arguments": ["status", "--short"]
  }
}
```

#### View commit history
```json
{
  "name": "shell_execute",
  "arguments": {
    "command": "git",
    "arguments": ["log", "--oneline", "-10"]
  }
}
```

## Security Considerations

### Path Validation

The shell execution service validates that all working directories are within allowed paths:
- The workspace directory (default: `/workspace`)
- The `/tmp` directory

Attempting to execute commands outside these paths will result in an `UnauthorizedAccessException`.

### Timeout Enforcement

All commands have configurable timeouts:
- Default: 30 seconds
- Maximum: 300 seconds (5 minutes)

Commands that exceed the timeout are automatically killed, including their entire process tree.

### No Shell Execution

Commands are executed directly without going through a shell interpreter, preventing shell injection attacks. Arguments are passed safely as an array rather than a string.

## Error Handling

### Command Not Found

If a command doesn't exist, you'll receive an error in the response:

```json
{
  "exitCode": -1,
  "stderr": "Execution failed: No such file or directory",
  "timedOut": false
}
```

### Timeout

If a command times out:

```json
{
  "exitCode": -1,
  "timedOut": true,
  "stderr": "",
  "executionTimeMs": 30000
}
```

### Permission Denied

If attempting to access unauthorized paths:

```json
{
  "error": "Working directory '/etc' is not within allowed paths"
}
```

## Best Practices

1. **Use appropriate timeouts** - Set longer timeouts for builds and tests
2. **Check exit codes** - A non-zero exit code indicates an error
3. **Parse stderr** - Error messages are typically in stderr
4. **Use working directories** - Execute commands in the correct context
5. **Chain simple commands** - Keep individual commands focused and simple

## Examples by Use Case

### Analyzing a .NET Solution

```json
// 1. List all projects
{
  "name": "shell_execute",
  "arguments": {
    "command": "find",
    "arguments": [".", "-name", "*.csproj"]
  }
}

// 2. Get project dependencies
{
  "name": "shell_execute",
  "arguments": {
    "command": "dotnet",
    "arguments": ["list", "package"]
  }
}

// 3. Search for specific patterns
{
  "name": "shell_execute",
  "arguments": {
    "command": "rg",
    "arguments": ["using System.Threading", "--count"]
  }
}
```

### Working with JSON Data

```json
// Execute and parse in one step
{
  "name": "shell_execute_json",
  "arguments": {
    "command": "dotnet",
    "arguments": ["build", "/t:GetProjectInfo", "/p:JsonOutput=true"]
  }
}
```

## Troubleshooting

### Command hangs
- Check if the command requires input (use non-interactive flags)
- Increase the timeout value
- Verify the command works in a regular shell first

### Permission errors
- Ensure the working directory is within allowed paths
- Check file permissions in the container
- Verify the command doesn't require root access

### Empty output
- Check the exit code - command may have failed
- Look at stderr for error messages
- Verify the command produces output to stdout (some commands use stderr)
