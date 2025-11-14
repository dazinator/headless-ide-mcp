# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["src/HeadlessIdeMcp.Server/HeadlessIdeMcp.Server.csproj", "HeadlessIdeMcp.Server/"]
COPY ["src/HeadlessIdeMcp.Core/HeadlessIdeMcp.Core.csproj", "HeadlessIdeMcp.Core/"]
COPY ["src/Directory.Build.props", "./"]
COPY ["src/Directory.Packages.props", "./"]
COPY ["src/.editorconfig", "./"]
COPY ["src/global.json", "./"]

# Restore dependencies
RUN dotnet restore "HeadlessIdeMcp.Server/HeadlessIdeMcp.Server.csproj"

# Copy source code
COPY src/HeadlessIdeMcp.Server/ HeadlessIdeMcp.Server/
COPY src/HeadlessIdeMcp.Core/ HeadlessIdeMcp.Core/

# Build the application
RUN dotnet build "HeadlessIdeMcp.Server/HeadlessIdeMcp.Server.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "HeadlessIdeMcp.Server/HeadlessIdeMcp.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Create workspace directory for mounted code
RUN mkdir -p /workspace

# Set environment variable for code base path
ENV CODE_BASE_PATH=/workspace

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HeadlessIdeMcp.Server.dll"]
