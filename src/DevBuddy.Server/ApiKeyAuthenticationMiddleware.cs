namespace DevBuddy.Server;

/// <summary>
/// Middleware for API key authentication
/// </summary>
public class ApiKeyAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;

    private const string ApiKeyHeaderName = "X-API-Key";

    public ApiKeyAuthenticationMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<ApiKeyAuthenticationMiddleware> logger)
    {
        _next = next;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip authentication for health check endpoint
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }

        // Check if API key authentication is enabled
        var apiKeyEnabled = _configuration.GetValue<bool>("Authentication:ApiKey:Enabled", false);
        
        if (!apiKeyEnabled)
        {
            await _next(context);
            return;
        }

        // Get configured API key
        var configuredApiKey = _configuration.GetValue<string>("Authentication:ApiKey:Key");
        
        if (string.IsNullOrWhiteSpace(configuredApiKey))
        {
            _logger.LogWarning("API key authentication is enabled but no key is configured");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new 
            { 
                error = "API key authentication is misconfigured" 
            });
            return;
        }

        // Get API key from request header
        if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var providedApiKey))
        {
            _logger.LogWarning("API key authentication failed: No API key provided. Client IP: {ClientIP}", 
                context.Connection.RemoteIpAddress);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new 
            { 
                error = "API key is required",
                header = ApiKeyHeaderName
            });
            return;
        }

        // Validate API key
        if (!string.Equals(configuredApiKey, providedApiKey, StringComparison.Ordinal))
        {
            _logger.LogWarning("API key authentication failed: Invalid API key. Client IP: {ClientIP}", 
                context.Connection.RemoteIpAddress);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new 
            { 
                error = "Invalid API key" 
            });
            return;
        }

        // API key is valid, proceed with request
        _logger.LogDebug("API key authentication successful. Client IP: {ClientIP}", 
            context.Connection.RemoteIpAddress);
        
        await _next(context);
    }
}

/// <summary>
/// Extension methods for API key authentication middleware
/// </summary>
public static class ApiKeyAuthenticationMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiKeyAuthenticationMiddleware>();
    }
}
