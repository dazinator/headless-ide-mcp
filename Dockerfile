# POC 5: Enhanced Container with DevContainer Base
# This Dockerfile uses the DevContainer base image for pre-configured developer environment

# Use DevContainer as base - includes vscode user, git, curl, jq, tree, nano, etc.
FROM mcr.microsoft.com/devcontainers/dotnet:1-8.0 AS base
WORKDIR /app

# Install ripgrep (only tool not included in DevContainer)
RUN apt-get update && apt-get install -y \
    ripgrep \
    && rm -rf /var/lib/apt/lists/*

# Create directory for HTTPS certificate
RUN mkdir -p /https && chown vscode:vscode /https

# Verify tool installations
RUN echo "=== Verifying CLI Tool Installations ===" && \
    echo "dotnet: $(dotnet --version)" && \
    echo "git: $(git --version)" && \
    echo "curl: $(curl --version | head -1)" && \
    echo "wget: $(wget --version | head -1)" && \
    echo "rg (ripgrep): $(rg --version | head -1)" && \
    echo "jq: $(jq --version)" && \
    echo "tree: $(tree --version | head -1)" && \
    echo "bash: $(bash --version | head -1)" && \
    echo "nano: $(nano --version | head -1)"

# Create workspace directory with correct ownership
RUN mkdir -p /workspace && chown vscode:vscode /workspace

# Set environment variables
ENV CODE_BASE_PATH=/workspace
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_Kestrel__Certificates__Default__Path=/https/aspnetapp.pfx
ENV ASPNETCORE_Kestrel__Certificates__Default__Password=DevCertPassword

# Expose ports
EXPOSE 8080
EXPOSE 8081

# Build stage (unchanged - uses dotnet/sdk for build)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["src/DevBuddy.Server/DevBuddy.Server.csproj", "DevBuddy.Server/"]
COPY ["src/DevBuddy.Core/DevBuddy.Core.csproj", "DevBuddy.Core/"]
COPY ["src/Directory.Build.props", "./"]
COPY ["src/Directory.Packages.props", "./"]
COPY ["src/.editorconfig", "./"]
COPY ["src/global.json", "./"]

# Restore dependencies
RUN dotnet restore "DevBuddy.Server/DevBuddy.Server.csproj"

# Copy source code
COPY src/DevBuddy.Server/ DevBuddy.Server/
COPY src/DevBuddy.Core/ DevBuddy.Core/

# Build the application
RUN dotnet build "DevBuddy.Server/DevBuddy.Server.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "DevBuddy.Server/DevBuddy.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage - combine DevContainer base with published app
FROM base AS final
WORKDIR /app

# Copy published application with correct ownership
COPY --from=publish --chown=vscode:vscode /app/publish .

# Copy entrypoint script that handles certificate setup at runtime
COPY --chown=vscode:vscode docker-entrypoint.sh /usr/local/bin/
RUN chmod +x /usr/local/bin/docker-entrypoint.sh

# Switch to non-root user (vscode user from DevContainer)
USER vscode

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["/usr/local/bin/docker-entrypoint.sh"]
