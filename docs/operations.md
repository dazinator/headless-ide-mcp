# Operations Guide

This document provides operational guidance for running, monitoring, and maintaining the DevBuddy server in production.

## Table of Contents

- [Deployment](#deployment)
- [Configuration](#configuration)
- [Resource Management](#resource-management)
- [Monitoring](#monitoring)
- [Audit Logging](#audit-logging)
- [Health Checks](#health-checks)
- [Troubleshooting](#troubleshooting)
- [Maintenance](#maintenance)

## Deployment

### Production Deployment with Docker Compose

```bash
# Production deployment
docker-compose up -d

# View logs
docker-compose logs -f

# Stop services
docker-compose down
```

### Environment Variables

| Variable | Description | Default | Required |
|----------|-------------|---------|----------|
| `ASPNETCORE_ENVIRONMENT` | Environment (Development/Production) | Production | No |
| `CODE_BASE_PATH` | Path to workspace in container | /workspace | Yes |
| `ASPNETCORE_HTTP_PORTS` | HTTP port | 8080 | No |
| `ASPNETCORE_HTTPS_PORTS` | HTTPS port | 8081 | No |

### Volume Mounts

**Development (Read-Write):**
```yaml
volumes:
  # Read-write mount allows agent to create/modify files for testing
  - ./your-codebase:/workspace
```

**Production (Read-Only):**
```yaml
volumes:
  # Read-only mount for security - prevents unauthorized modifications
  - ./your-codebase:/workspace:ro
```

**Important**: 
- Use **read-write** (default) for development/testing environments where the agent needs to create files, write tests, or modify code
- Use **read-only** (`:ro` flag) for production environments to prevent unauthorized modifications

## Configuration

### Application Settings

Configuration is managed through `appsettings.json` and `appsettings.{Environment}.json` files.

#### Production Configuration

`appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "DevBuddy.Core.ProcessExecution.CommandExecutionService": "Information"
    }
  },
  "AllowedHosts": "*",
  "CommandExecution": {
    "MaxTimeoutSeconds": 300,
    "AllowedPaths": [
      "/workspace",
      "/tmp"
    ],
    "AllowedCommands": [
      "dotnet",
      "git",
      "rg",
      "jq",
      "tree",
      "bash",
      "curl",
      "find"
    ],
    "DeniedCommands": [
      "rm",
      "dd",
      "mkfs",
      "fdisk",
      "format",
      "shutdown",
      "reboot",
      "init"
    ],
    "SanitizeErrorMessages": true,
    "EnableAuditLogging": true
  }
}
```

### Configuration Override

Use environment-specific configuration files to override settings:

```bash
# Development
export ASPNETCORE_ENVIRONMENT=Development

# Staging
export ASPNETCORE_ENVIRONMENT=Staging

# Production
export ASPNETCORE_ENVIRONMENT=Production
```

## Resource Management

### CPU and Memory Limits

Resource limits are configured in `docker-compose.yml`:

```yaml
deploy:
  resources:
    limits:
      cpus: '2.0'      # Maximum 2 CPU cores
      memory: 1G       # Maximum 1GB RAM
    reservations:
      memory: 512M     # Reserved 512MB RAM
```

### Adjusting Resource Limits

For different workloads, adjust limits based on requirements:

**Light Workload** (small codebases, few concurrent requests):
```yaml
limits:
  cpus: '1.0'
  memory: 512M
reservations:
  memory: 256M
```

**Heavy Workload** (large codebases, many concurrent requests):
```yaml
limits:
  cpus: '4.0'
  memory: 2G
reservations:
  memory: 1G
```

### Monitoring Resource Usage

#### Check Container Resource Usage

```bash
# Real-time stats
docker stats devbuddy-server

# One-time snapshot
docker stats --no-stream devbuddy-server
```

Example output:
```
CONTAINER ID   NAME                     CPU %     MEM USAGE / LIMIT   MEM %
a1b2c3d4e5f6   devbuddy-server  5.23%     256MiB / 1GiB      25.00%
```

#### Check Resource Limit Events

```bash
# View OOM events
docker inspect devbuddy-server | grep -i oom

# View restart count
docker inspect devbuddy-server --format='{{.RestartCount}}'
```

### OOM (Out of Memory) Handling

The container is configured to restart on OOM:

```yaml
restart: unless-stopped
```

**When OOM occurs:**
1. Container automatically restarts
2. Check logs for memory-intensive operations
3. Consider increasing memory limits
4. Investigate memory leaks in application

## Monitoring

### Health Check

The server provides a health check endpoint:

```bash
# Check health
curl http://localhost:5000/health

# Expected response
{
  "status": "healthy",
  "codeBasePath": "/workspace"
}
```

### Health Check in Docker

Health check is configured in Dockerfile:

```dockerfile
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1
```

Check health status:
```bash
docker inspect --format='{{.State.Health.Status}}' devbuddy-server
```

### Log Monitoring

#### View Application Logs

```bash
# Follow logs
docker-compose logs -f devbuddy

# Last 100 lines
docker-compose logs --tail=100 devbuddy

# Since specific time
docker-compose logs --since=1h devbuddy
```

#### Filter Audit Logs

Audit logs use structured logging. Filter by correlation ID:

```bash
docker-compose logs devbuddy | grep "CorrelationId: a1b2c3d4"
```

### Metrics Collection

For production monitoring, integrate with:

- **Prometheus**: Metrics collection
- **Grafana**: Visualization
- **ELK Stack**: Log aggregation
- **Datadog**: APM and monitoring

Example metrics to track:
- Request rate
- Command execution duration
- Error rate
- Resource utilization
- Timeout frequency

## Audit Logging

### Log Structure

Audit logs are written in structured format for easy parsing and analysis.

#### Log Entry Example

```
info: DevBuddy.Core.ProcessExecution.CommandExecutionService[0]
      Command execution Completed: dotnet --version (CorrelationId: abc-123, User: client-1, ExitCode: 0, Duration: 250ms)
```

### Querying Audit Logs

#### Find All Failed Commands

```bash
docker-compose logs devbuddy | grep "Status: Failed"
```

#### Find Commands by User

```bash
docker-compose logs devbuddy | grep "User: mcp-client"
```

#### Find Long-Running Commands

```bash
docker-compose logs devbuddy | grep "Duration:" | awk '{print $(NF-1), $NF}' | sort -n
```

### Log Retention

Configure log retention in Docker daemon:

`/etc/docker/daemon.json`:
```json
{
  "log-driver": "json-file",
  "log-opts": {
    "max-size": "10m",
    "max-file": "10"
  }
}
```

For production, consider:
- **Max size**: 10-50MB per file
- **Max files**: 10-30 rotated files
- **Total retention**: 90-365 days
- **External storage**: Ship logs to centralized system

### Centralized Logging

For production, send logs to centralized system:

**Option 1: Fluentd/Fluent Bit**
```yaml
logging:
  driver: fluentd
  options:
    fluentd-address: localhost:24224
    tag: devbuddy
```

**Option 2: Syslog**
```yaml
logging:
  driver: syslog
  options:
    syslog-address: "tcp://192.168.0.10:514"
```

**Option 3: JSON File with External Processing**
```yaml
logging:
  driver: json-file
  options:
    max-size: "10m"
    max-file: "10"
```

## Health Checks

### Liveness Probe

Checks if the container is running:

```bash
curl -f http://localhost:5000/health || exit 1
```

### Readiness Probe

Checks if the service can accept requests:

```bash
# Check if MCP server responds
curl -X POST http://localhost:5000/ \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
```

### Kubernetes Health Checks

For Kubernetes deployments:

```yaml
livenessProbe:
  httpGet:
    path: /health
    port: 8080
  initialDelaySeconds: 10
  periodSeconds: 30

readinessProbe:
  httpGet:
    path: /health
    port: 8080
  initialDelaySeconds: 5
  periodSeconds: 10
```

## Troubleshooting

### Common Issues

#### Container Won't Start

**Check logs:**
```bash
docker-compose logs devbuddy
```

**Common causes:**
- Port already in use
- Invalid configuration
- Missing environment variables
- Volume mount permissions

#### High Memory Usage

**Symptoms:**
- Container restarts frequently
- OOM errors in logs

**Solutions:**
1. Check for memory leaks in audit logs
2. Increase memory limits
3. Reduce concurrent operations
4. Lower timeout values

#### Commands Timing Out

**Symptoms:**
- Frequent timeout messages in logs
- Slow response times

**Solutions:**
1. Check system resources (CPU, memory)
2. Increase timeout limits
3. Investigate slow commands in logs
4. Check network latency

#### Permission Denied Errors

**Symptoms:**
- `UnauthorizedAccessException` in logs
- Commands fail to execute

**Solutions:**
1. Verify `AllowedPaths` configuration
2. Check file permissions on mounted volumes
3. Verify user context (should be `vscode`)

### Debug Mode

Enable verbose logging for troubleshooting:

`appsettings.Development.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "HeadlessIdeMcp": "Trace"
    }
  }
}
```

### Performance Profiling

Profile command execution:

```bash
# Enable trace logging
export ASPNETCORE_ENVIRONMENT=Development

# Watch execution times in logs
docker-compose logs -f devbuddy | grep "ExecutionTimeMs"
```

## Maintenance

### Regular Tasks

#### Daily
- Review error logs
- Check resource utilization
- Verify health check status

#### Weekly
- Analyze audit logs for anomalies
- Review command execution patterns
- Check for configuration drift

#### Monthly
- Update base image and dependencies
- Review and update security configuration
- Capacity planning based on usage trends
- Scan for CVEs

### Updates and Upgrades

#### Update Container Image

```bash
# Pull latest image
docker-compose pull

# Recreate containers
docker-compose up -d --force-recreate

# Verify
docker-compose ps
docker-compose logs -f
```

#### Update Configuration

```bash
# Edit configuration
vim src/DevBuddy.Server/appsettings.json

# Rebuild and restart
docker-compose up -d --build
```

### Backup and Recovery

#### Configuration Backup

```bash
# Backup configuration
tar -czf config-backup-$(date +%Y%m%d).tar.gz \
  src/DevBuddy.Server/appsettings*.json \
  docker-compose.yml
```

#### Log Backup

```bash
# Export logs
docker-compose logs > logs-backup-$(date +%Y%m%d).log
```

### Scaling

For higher load, consider:

**Horizontal Scaling**: Run multiple instances with load balancer
```yaml
services:
  devbuddy:
    deploy:
      replicas: 3
```

**Vertical Scaling**: Increase resources per instance
```yaml
deploy:
  resources:
    limits:
      cpus: '4.0'
      memory: 4G
```

---

**Last Updated**: 2024-11-15  
**Version**: Phase 2 - Production Hardening
