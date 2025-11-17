#!/bin/bash
# Docker entrypoint script for Headless IDE MCP
# Handles HTTPS certificate setup at runtime with support for:
# 1. Using local dev cert from host (mounted at /https-host)
# 2. Using existing cert from volume (persisted from previous run)
# 3. Generating new cert and persisting to volume

set -e

CERT_PATH="/https/aspnetapp.pfx"
HOST_CERT_PATH="/https-host/aspnetapp.pfx"
CERT_PASSWORD="${ASPNETCORE_Kestrel__Certificates__Default__Password:-DevCertPassword}"

echo "=== Headless IDE MCP - HTTPS Certificate Setup ==="

# Check if local dev cert from host is mounted
if [ -f "$HOST_CERT_PATH" ]; then
    echo "✓ Found local dev cert from host machine at $HOST_CERT_PATH"
    echo "  Copying to container..."
    cp "$HOST_CERT_PATH" "$CERT_PATH"
    chmod 644 "$CERT_PATH"
    echo "  ✓ Using local dev cert from host"
# Check if cert already exists in volume (from previous run)
elif [ -f "$CERT_PATH" ]; then
    echo "✓ Found existing certificate in volume at $CERT_PATH"
    echo "  ✓ Using persisted certificate from previous run"
# Generate new certificate if none exists
else
    echo "○ No existing certificate found"
    echo "  Generating new self-signed development certificate..."
    
    # Clean any existing dev certs
    dotnet dev-certs https --clean
    
    # Generate new certificate
    dotnet dev-certs https -ep "$CERT_PATH" -p "$CERT_PASSWORD" --trust
    
    # Set appropriate permissions
    chmod 644 "$CERT_PATH"
    
    echo "  ✓ New certificate generated and saved to $CERT_PATH"
    echo "  ℹ Certificate will persist across container restarts via Docker volume"
fi

echo "=== Certificate Setup Complete ==="
echo "Certificate location: $CERT_PATH"
echo ""

# Configure Git Authentication
echo "=== Git Authentication Setup ==="

# Set git credential helper to use store (credentials stored in ~/.git-credentials)
git config --global credential.helper store

# Check if git credentials file is already mounted/present
if [ -f ~/.git-credentials ] && [ -s ~/.git-credentials ]; then
    echo "✓ Existing git credentials file detected"
    echo "  Skipping credential configuration from environment variables"
    echo "  Using pre-configured credentials from mounted file"
    chmod 600 ~/.git-credentials
    echo "  ℹ Git credentials file secured with 600 permissions"
else
    # No existing credentials file, configure from environment variables
    echo "○ No existing credentials file found"
    echo "  Configuring credentials from environment variables"
    
    # Determine the git username (use environment variable or default to 'vscode' to match container user)
    GIT_USER="${GIT_USERNAME:-vscode}"
    echo "  Git username: $GIT_USER"

    # Configure GitHub credentials if GITHUB_PAT is provided
    if [ -n "$GITHUB_PAT" ]; then
        echo "  ✓ GitHub PAT detected - configuring credentials"
        # Store credentials for github.com
        echo "https://${GIT_USER}:${GITHUB_PAT}@github.com" >> ~/.git-credentials
        echo "    ✓ GitHub credentials configured"
    else
        echo "  ○ No GITHUB_PAT provided - GitHub authentication not configured"
    fi

    # Configure Azure DevOps credentials if AZDO_PAT is provided
    if [ -n "$AZDO_PAT" ]; then
        echo "  ✓ Azure DevOps PAT detected - configuring credentials"
        # Store credentials for dev.azure.com
        echo "https://${GIT_USER}:${AZDO_PAT}@dev.azure.com" >> ~/.git-credentials
        echo "    ✓ Azure DevOps credentials configured"
    else
        echo "  ○ No AZDO_PAT provided - Azure DevOps authentication not configured"
    fi

    # Set secure permissions on credentials file if it exists
    if [ -f ~/.git-credentials ]; then
        chmod 600 ~/.git-credentials
        echo "  ℹ Git credentials configured and secured"
    fi
fi

echo "=== Git Authentication Setup Complete ==="
echo ""
echo "Starting application..."
echo ""

# Execute the main application
exec dotnet HeadlessIdeMcp.Server.dll
