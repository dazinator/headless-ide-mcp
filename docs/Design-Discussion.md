
---

devbuddy

Design Document (Revised for CLI-first Architecture)

1. Overview

devbuddy is a custom Model Context Protocol (MCP) server that provides a headless, containerised development environment for AI agents. It exposes a controlled runtime where the agent can execute arbitrary CLI commands, explore the codebase, query project structure, interact with LSP tools, and call higher level domain specific tools implemented in .NET.

This approach mirrors the operational model used by GitHub Copilot Agents on GitHub Actions runners. The agent has access to a predefined set of CLI tools on the container's PATH, along with a standardised MCP tool for executing commands. The agent discovers the available capabilities and composes them at runtime.

The server is built using ASP.NET Core, with tool logic residing in the HeadlessIdeMcp.Core project. It is designed to be modular, safe, and easily extensible.


---

2. Architectural Principles

2.1 CLI-first capabilities

Expose a general purpose command execution tool:

shell.execute

shell.executeJson (optional extension)


This gives the agent the power to use:

dotnet CLI (list projects, build, test)

ripgrep (source search)

tree, find, sed, jq

your own custom analysis tools

language servers indirectly

any other utilities included in the container


This dramatically reduces the number of MCP specific tools needed and matches the model used by Copilot Agents.

2.2 High level domain tools alongside CLI

Provide a small set of structured tools for things that benefit from returning clean JSON:

project graph

DI graph

architectural constraints

task breakdown helpers

work item context parsing


The CLI handles the low level mechanics. The structured tools encode higher level semantics.

2.3 LSP integration for semantic navigation

A separate container running OmniSharp plus lsp-mcp can be included. This provides:

find symbol

find references

diagnostics

symbol maps


The agent will naturally combine LSP tools with shell tools.

2.4 Stateless server

The server is not tied to any workflow engine. It acts only as a capability provider.


---

3. Repository Structure

Your existing repo structure is appropriate and supports this architecture:

devbuddy/
├── src/
│   ├── HeadlessIdeMcp.Server/           # ASP.NET Core MCP server with CLI tools
│   ├── HeadlessIdeMcp.Core/             # Higher level analysis libraries
│   ├── HeadlessIdeMcp.IntegrationTests/ # End to end tests
│   └── Solution.sln
├── sample-codebase/
│   ├── SampleProject1/
│   ├── SampleProject2/
│   └── SampleCodeBase.sln
├── docs/
├── docker-compose.yml
├── docker-compose.dcproj
└── Dockerfile

This layout supports:

server code

shared logic in .Core

sample solution for development and integration testing

documentation

container build and orchestration



---

4. Core MCP Tools

4.1 shell.execute

Executes a CLI command in a sandbox.

Input

{
  "command": "string",
  "cwd": "optional string",
  "timeoutSeconds": 30
}

Output

{
  "stdout": "...",
  "stderr": "...",
  "exitCode": 0
}

4.2 shell.executeJson

Useful when calling tools that return JSON.

Output

{
  "json": { ... },
  "stderr": "...",
  "exitCode": 0
}

4.3 dotnet.projectGraph

Uses Roslyn or MSBuild APIs to return:

list of projects

references

source file enumeration

output paths

target frameworks


4.4 dotnet.diGraph

Parses DI container usage to produce:

service registration list

service types

lifetimes

implementation mapping


4.5 dotnet.suggestRelevantFiles

Combines heuristics with optional shell calls (ripgrep) to propose:

code files possibly relevant to a natural language query


4.6 dotnet.proposeTaskBreakdown

Generates a structured skeleton of tasks for a change request.

4.7 policy.validateCodingRules

Runs a set of rules that can include:

custom analyzers

conventions

layered architecture checks


These high level structured tools are optional. The CLI tool can always be used instead, but these tools provide better data for the model.


---

5. Container Environment

The container should include:

CLI tools:

dotnet SDK and runtime

ripgrep (rg)

jq

tree

bash

findutils

coreutils

optionally git

optionally curl and wget

any custom .NET global tools you create


Workspace

Git synced repo mounted at /repo


User privileges

Non root user

No dangerous capabilities

Ideally no outbound network access


Shell environment

PATH should include:

/usr/bin

/usr/local/bin

/repo/tools (for your custom utilities)


This gives the agent a powerful yet controlled environment.


---

6. Docker Compose Design

Suggested services:

services:
  git-sync:
    ... keeps /repo up to date ...

  lsp-server:
    image: omnisharp
    volumes:
      - repo:/repo

  lsp-mcp:
    image: your-lsp-mcp
    volumes:
      - repo:/repo

  devbuddy:
    build: .
    volumes:
      - repo:/repo

The MCP server does not need to communicate directly with LSP. Claude coordinates tool usage.


---

7. Workflow for Agent Usage

Example process when Claude needs to understand a work item:

1. shell.execute: "dotnet sln list"


2. shell.execute: "rg FooService -g '*.cs'"


3. lsp.findReferences: "IOrderService"


4. shell.execute: "dotnet build"


5. dotnet.projectGraph


6. dotnet.diGraph


7. dotnet.proposeTaskBreakdown



This demonstrates how the agent chains multiple tools without your MCP layer needing explicit orchestration logic.


---

8. Security Considerations

CLI execution increases power, but also requires good sandbox discipline:

run inside a rootless Docker container

restrict filesystem access to /repo and /tmp

disable network access where possible

avoid mounting host sensitive paths

place global tools in controlled locations

enforce timeouts


This matches the GitHub runner model.


---

9. Development Roadmap

Phase 1

Implement shell.execute

Implement server skeleton

Add container with CLI tools

Add integration tests for shell execution


Phase 2

Add project graph and DI graph structured tools

Add msbuild or Roslyn workspace loader

Add sample codebase tests


Phase 3

Add task breakdown and heuristic tools

Add policy validation tools

Expand container toolset as needed


Phase 4

LSP server integration via lsp-mcp

Optional interactive session tools

Additional custom analyzers or formatters



---

10. Summary

This revised design provides a flexible, powerful foundation:

shell-first architecture for maximum capability

high level .NET tools for structured reasoning

compatibility with LSP via lsp-mcp

safe sandboxed container environment

modular design aligned with your existing repo structure


The agent receives an environment similar to GitHub Actions and Copilot Agents, without the complexity of large amounts of custom MCP tool code.


---
