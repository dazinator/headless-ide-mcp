# Build Notes

## Docker Build

The Docker build has been configured and tested. If you encounter network issues during the build (e.g., unable to access NuGet), ensure your Docker daemon has internet access and can reach `api.nuget.org`.

The Dockerfile uses multi-stage builds:
1. **Build stage**: Restores and compiles the application
2. **Publish stage**: Creates release artifacts
3. **Runtime stage**: Creates minimal runtime image

## Local Development vs Docker

When developing locally:
- Run `dotnet run` from the `src/HeadlessIdeMcp.Server` directory
- Set `CODE_BASE_PATH` environment variable to point to the code you want to analyze
- The server binds to a port specified in `Properties/launchSettings.json` (if exists) or defaults

When running in Docker:
- The `CODE_BASE_PATH` is set to `/workspace`
- The `sample-codebase` directory is mounted to `/workspace` via docker-compose
- The server exposes port 8080 internally, mapped to 5000 on the host

## Testing the Server

Use the provided `.http/test-mcp-server.http` file with:
- Visual Studio 2022 (built-in HTTP client)
- Visual Studio Code (with REST Client extension)
- JetBrains Rider
- curl or any HTTP client

The MCP endpoints are mapped to the root path `/` by default.
