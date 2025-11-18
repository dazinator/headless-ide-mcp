using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using Shouldly;
using Microsoft.Extensions.Configuration;

namespace DevBuddy.IntegrationTests;

/// <summary>
/// Integration tests for API key authentication
/// </summary>
public class ApiKeyAuthenticationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiKeyAuthenticationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthEndpoint_WithoutApiKey_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthEndpoint_WithAuthEnabled_WithoutApiKey_ReturnsOk()
    {
        // Arrange - Health endpoint should always be accessible
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Authentication:ApiKey:Enabled", "true");
            builder.UseSetting("Authentication:ApiKey:Key", "test-api-key");
        }).CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert - Health check should work without API key
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task McpEndpoint_WhenAuthEnabled_WithoutApiKey_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Authentication:ApiKey:Enabled", "true");
            builder.UseSetting("Authentication:ApiKey:Key", "test-api-key");
        }).CreateClient();

        var request = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/list"
        };

        // Act
        var response = await client.PostAsJsonAsync("/", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task McpEndpoint_WhenAuthEnabled_WithInvalidApiKey_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Authentication:ApiKey:Enabled", "true");
            builder.UseSetting("Authentication:ApiKey:Key", "correct-api-key");
        }).CreateClient();

        client.DefaultRequestHeaders.Add("X-API-Key", "wrong-api-key");

        var request = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/list"
        };

        // Act
        var response = await client.PostAsJsonAsync("/", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task McpEndpoint_WhenAuthEnabled_WithValidApiKey_DoesNotReturnUnauthorized()
    {
        // Arrange
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Authentication:ApiKey:Enabled", "true");
            builder.UseSetting("Authentication:ApiKey:Key", "correct-api-key");
        }).CreateClient();

        client.DefaultRequestHeaders.Add("X-API-Key", "correct-api-key");

        var request = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/list"
        };

        // Act
        var response = await client.PostAsJsonAsync("/", request);

        // Assert - Should pass authentication (actual MCP response may vary)
        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }
}
