# Testing Git Authentication

This document provides instructions for testing the git authentication functionality in the Headless IDE MCP container.

## Prerequisites

1. Personal Access Tokens created for:
   - GitHub (from https://github.com/settings/tokens)
   - Azure DevOps (from https://dev.azure.com/{org}/_usersSettings/tokens)

2. A test repository accessible with your tokens (for verification)

## Setup for Testing

1. Create a `.env` file from the example:
   ```bash
   cp .env.example .env
   ```

2. Edit `.env` with your actual tokens:
   ```bash
   GIT_USERNAME=your-username
   GITHUB_PAT=ghp_your_actual_token
   AZDO_PAT=your_actual_azdo_token  # Optional if testing Azure DevOps
   ```

3. Start the container:
   ```bash
   docker-compose up --build
   ```

## Verification Steps

### 1. Check Container Startup Logs

When the container starts, you should see:

```
=== Git Authentication Setup ===
Git username: your-username
✓ GitHub PAT detected - configuring credentials
  ✓ GitHub credentials configured
✓ Azure DevOps PAT detected - configuring credentials
  ✓ Azure DevOps credentials configured
  ℹ Git credentials configured and secured
=== Git Authentication Setup Complete ===
```

### 2. Verify Git Configuration

Connect to the running container:

```bash
docker exec -it headless-ide-mcp-server bash
```

Check git credential helper is configured:

```bash
git config --get credential.helper
# Should output: store
```

Check credentials file exists with proper permissions:

```bash
ls -la ~/.git-credentials
# Should show: -rw------- (600 permissions)
```

Verify credentials are stored (tokens will be visible):

```bash
cat ~/.git-credentials
# Should show entries like:
# https://username:token@github.com
# https://username:token@dev.azure.com
```

### 3. Test Git Operations

#### Test GitHub Access

Using a repository you have access to:

```bash
docker exec -it headless-ide-mcp-server git ls-remote https://github.com/your-username/your-repo.git
```

This should list the refs without prompting for credentials.

#### Test Clone Operation

```bash
docker exec -it headless-ide-mcp-server bash -c '
cd /tmp && 
git clone https://github.com/your-username/your-repo.git test-clone &&
cd test-clone &&
git log -1 --oneline &&
cd .. &&
rm -rf test-clone
'
```

#### Test Azure DevOps (if configured)

```bash
docker exec -it headless-ide-mcp-server git ls-remote https://dev.azure.com/your-org/your-project/_git/your-repo
```

### 4. Test via MCP Shell Execute Tool

Send a request to the MCP server to execute a git command:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "shell_execute",
    "arguments": {
      "command": "git",
      "arguments": ["ls-remote", "https://github.com/your-username/your-repo.git"]
    }
  }
}
```

The response should contain the repository refs without any authentication errors.

### 5. Verify Credential Redaction in Logs

Check that credentials are not exposed in logs:

```bash
docker logs headless-ide-mcp-server 2>&1 | grep -i "token\|password\|pat"
```

You should NOT see any actual token values. If credentials appear in URLs, they should be redacted as `***REDACTED***`.

## Troubleshooting Tests

### Authentication Fails

1. **Verify environment variables are set:**
   ```bash
   docker exec -it headless-ide-mcp-server env | grep -E "GITHUB_PAT|AZDO_PAT"
   ```
   
2. **Check token scopes:**
   - GitHub: Ensure token has `repo` scope
   - Azure DevOps: Ensure token has `Code (Read)` or `Code (Read & Write)` scope

3. **Verify token is not expired:**
   - Check token expiration in GitHub/Azure DevOps settings
   - Regenerate if necessary and update `.env` file

### Credentials File Not Created

If `~/.git-credentials` doesn't exist:

1. Check that environment variables are passed to container:
   ```bash
   docker-compose config | grep -E "GITHUB_PAT|AZDO_PAT"
   ```

2. Verify entrypoint script is executing:
   ```bash
   docker logs headless-ide-mcp-server 2>&1 | head -50
   ```

### Permission Denied

If you see permission errors:

1. Check file permissions:
   ```bash
   docker exec -it headless-ide-mcp-server stat -c '%a %U:%G' ~/.git-credentials
   ```
   Should output: `600 vscode:vscode`

2. Verify running as vscode user:
   ```bash
   docker exec -it headless-ide-mcp-server whoami
   ```
   Should output: `vscode`

## Security Testing

### Ensure Tokens Are Not Exposed

1. **Check MCP responses don't contain tokens:**
   - Execute git commands via MCP shell_execute
   - Verify responses don't contain PAT values
   - Check for proper redaction in error messages

2. **Verify audit logs redact credentials:**
   ```bash
   docker logs headless-ide-mcp-server 2>&1 | grep "git clone"
   ```
   URLs should show `***REDACTED***` instead of actual tokens

3. **Confirm credentials aren't in Docker image:**
   ```bash
   docker history headless-ide-mcp:dev | grep -i "token\|pat"
   ```
   Should find nothing (credentials are runtime-configured, not baked in)

## Automated Test Script

Save this as `test-git-auth.sh`:

```bash
#!/bin/bash
# Automated test script for git authentication

set -e

echo "=== Testing Git Authentication in Container ==="

# Check container is running
if ! docker ps | grep -q headless-ide-mcp-server; then
    echo "❌ Container is not running. Start with: docker-compose up"
    exit 1
fi

echo "✓ Container is running"

# Check git config
CRED_HELPER=$(docker exec headless-ide-mcp-server git config --get credential.helper)
if [ "$CRED_HELPER" != "store" ]; then
    echo "❌ Git credential helper not configured correctly: $CRED_HELPER"
    exit 1
fi
echo "✓ Git credential helper is configured: $CRED_HELPER"

# Check credentials file exists
if ! docker exec headless-ide-mcp-server test -f /home/vscode/.git-credentials; then
    echo "❌ Credentials file does not exist"
    exit 1
fi
echo "✓ Credentials file exists"

# Check file permissions
PERMS=$(docker exec headless-ide-mcp-server stat -c '%a' /home/vscode/.git-credentials)
if [ "$PERMS" != "600" ]; then
    echo "❌ Incorrect permissions on credentials file: $PERMS (expected 600)"
    exit 1
fi
echo "✓ Credentials file has correct permissions: $PERMS"

# Test GitHub access (requires GITHUB_PAT and a valid test repo)
if [ -n "$TEST_GITHUB_REPO" ]; then
    echo "Testing GitHub access with repo: $TEST_GITHUB_REPO"
    if docker exec headless-ide-mcp-server git ls-remote "$TEST_GITHUB_REPO" > /dev/null 2>&1; then
        echo "✓ GitHub authentication successful"
    else
        echo "❌ GitHub authentication failed"
        exit 1
    fi
else
    echo "⚠ Skipping GitHub test (set TEST_GITHUB_REPO env var to test)"
fi

echo ""
echo "✅ All tests passed!"
```

Run with:
```bash
chmod +x test-git-auth.sh
TEST_GITHUB_REPO=https://github.com/your-username/your-repo.git ./test-git-auth.sh
```

## Expected Results

✅ **Success Indicators:**
- Container starts without errors
- Git credential helper is configured
- Credentials file exists with 600 permissions
- Git operations succeed without prompting for credentials
- Credentials are redacted in logs and responses

❌ **Failure Indicators:**
- Authentication errors when accessing repos
- Prompts for username/password
- Credentials visible in logs or responses
- Missing or incorrectly configured credential helper

---

**Last Updated**: 2024-11-17  
**Version**: Phase 2 - Git Authentication Support
