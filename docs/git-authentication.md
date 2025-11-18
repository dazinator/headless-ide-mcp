# Git Authentication in Dev Container

This document explains how to configure git authentication within the Headless IDE MCP container to enable git operations with remote repositories (GitHub, Azure DevOps, etc.) without exposing credentials to AI agents.

## Overview

The container supports secure git authentication using Personal Access Tokens (PATs) configured via environment variables. The credentials are set up automatically at container startup and are never exposed to MCP tools or AI agents.

## How It Works

1. **Environment Variables**: You provide PAT tokens via environment variables
2. **Git Credential Helper**: The container automatically configures git credential helper at startup
3. **Transparent Authentication**: Git operations automatically use the configured credentials
4. **Security**: Credentials are never logged, exposed in MCP responses, or visible to AI agents

## Supported Services

- **GitHub**: Use `GITHUB_PAT` environment variable
- **Azure DevOps**: Use `AZDO_PAT` environment variable
- **Generic Git**: Use `GIT_USERNAME` for the username (defaults to your system username if not provided)

## Setup Instructions

### 1. Create Personal Access Tokens

#### GitHub
1. Go to GitHub Settings → Developer settings → Personal access tokens → Tokens (classic)
2. Click "Generate new token (classic)"
3. Select scopes:
   - `repo` (for private repositories)
   - `workflow` (if you need to modify workflows)
4. Generate and copy the token

#### Azure DevOps
1. Go to User Settings → Personal access tokens
2. Click "New Token"
3. Select scopes:
   - `Code (Read & Write)` for repository access
4. Generate and copy the token

### 2. Configure Environment Variables

Create a `.env` file in the project root (this file is git-ignored for security):

```bash
# Git user configuration
GIT_USERNAME=your-username

# GitHub Personal Access Token
GITHUB_PAT=ghp_your_github_token_here

# Azure DevOps Personal Access Token  
AZDO_PAT=your_azdo_token_here
```

### 3. Use docker-compose with .env file

The docker-compose.yml automatically loads environment variables from the .env file:

```bash
docker-compose up --build
```

### Alternative: Set Environment Variables Directly

You can also set environment variables directly when running docker-compose:

```bash
GITHUB_PAT=ghp_xxx AZDO_PAT=yyy docker-compose up
```

Or export them in your shell:

```bash
export GIT_USERNAME=your-username
export GITHUB_PAT=ghp_your_token
export AZDO_PAT=your_azdo_token
docker-compose up
```

### Alternative: Mount Pre-configured Credentials File

If you already have a git credentials file on your host machine, you can mount it directly into the container instead of using environment variables:

1. Ensure your credentials file exists at `~/.git-credentials` on your host machine with entries like:
   ```
   https://username:token@github.com
   https://username:token@dev.azure.com
   ```

2. Uncomment the volume mount in `docker-compose.yml`:
   ```yaml
   volumes:
     - ~/.git-credentials:/home/vscode/.git-credentials:ro
   ```

3. Start the container:
   ```bash
   docker-compose up --build
   ```

**Note:** When a credentials file is mounted, the entrypoint script detects it and skips configuration from environment variables, preserving your existing credentials.

## How Credentials Are Stored

The container uses git's credential helper to store credentials securely:

1. **In-Memory Store**: Credentials are stored in the container's file system at `/home/vscode/.git-credentials`
2. **Automatic Cleanup**: Credentials are removed when the container is destroyed
3. **Not Persisted**: Credentials are NOT persisted in Docker volumes or images (unless explicitly mounted)
4. **Format**: Credentials are stored in git's standard format: `https://username:token@hostname`

**Two Configuration Methods:**
- **Environment Variables** (default): Credentials generated from `GITHUB_PAT` and `AZDO_PAT` at startup
- **Mounted File** (alternative): Pre-existing credentials file mounted from host machine

## Verification

Once configured, you can test git authentication:

### Using MCP Shell Execute Tool

```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "shell_execute",
    "arguments": {
      "command": "git",
      "arguments": ["ls-remote", "https://github.com/user/repo.git"]
    }
  }
}
```

### Using Docker Exec

```bash
docker exec -it headless-ide-mcp-server git ls-remote https://github.com/user/repo.git
```

## Security Considerations

### What Gets Protected

✅ **Protected (Not Exposed):**
- PAT tokens are never in source control
- PAT tokens are never logged by the application
- PAT tokens are never returned in MCP tool responses
- PAT tokens are automatically redacted from audit logs
- Credentials are isolated within the container

### What Gets Logged

The audit logs will show git commands but with credentials redacted:

```
Command execution: git clone https://***REDACTED***@github.com/user/repo.git
```

### Best Practices

1. **Use .env file**: Keep credentials in `.env` (git-ignored) for local development
2. **Rotate tokens regularly**: Generate new PATs periodically
3. **Minimal scopes**: Only grant necessary permissions to tokens
4. **Separate tokens**: Use different tokens for different services
5. **Monitor usage**: Review git operations in audit logs
6. **Environment-specific**: Use different tokens for dev/staging/production

## Troubleshooting

### Authentication Fails

If git operations fail with authentication errors:

1. **Check environment variables are set**:
   ```bash
   docker exec -it headless-ide-mcp-server env | grep -E "GIT_|GITHUB_|AZDO_"
   ```

2. **Verify credential helper is configured**:
   ```bash
   docker exec -it headless-ide-mcp-server git config --get credential.helper
   ```
   Should output: `store`

3. **Check credentials file**:
   ```bash
   docker exec -it headless-ide-mcp-server cat /home/vscode/.git-credentials
   ```
   Should show entries like: `https://username:token@github.com`

### Token Scope Issues

If you can read but not write:
- Ensure your token has write permissions
- For GitHub: Check `repo` scope is enabled
- For Azure DevOps: Check `Code (Write)` scope is enabled

### URL Format Issues

Make sure you're using HTTPS URLs:
- ✅ `https://github.com/user/repo.git`
- ❌ `git@github.com:user/repo.git` (SSH not supported with this credential method)

## Limitations

1. **HTTPS Only**: This method works with HTTPS git URLs, not SSH
2. **Container Scope**: Credentials only exist within the container
3. **No Interactive Prompts**: Authentication must be automatic (no interactive credential prompts)

## Advanced Configuration

### Multiple GitHub Accounts

If you need different credentials for different repositories, you can configure git credential helpers per-URL:

```bash
docker exec -it headless-ide-mcp-server bash -c '
git config --global credential.https://github.com/org1.helper "store --file ~/.git-credentials-org1"
git config --global credential.https://github.com/org2.helper "store --file ~/.git-credentials-org2"
'
```

Then provide multiple environment variables:
```bash
GITHUB_ORG1_PAT=token1
GITHUB_ORG2_PAT=token2
```

### Custom Git Credential Helper

For more advanced scenarios, you can mount a custom git credential helper script:

```yaml
volumes:
  - ./custom-git-credential-helper:/usr/local/bin/git-credential-custom:ro
```

Then configure git to use it in the entrypoint script.

## References

- [Git Credential Storage](https://git-scm.com/book/en/v2/Git-Tools-Credential-Storage)
- [GitHub Personal Access Tokens](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/creating-a-personal-access-token)
- [Azure DevOps Personal Access Tokens](https://learn.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate)

---

**Last Updated**: 2024-11-17  
**Version**: Phase 2 - Git Authentication Support
