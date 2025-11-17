using System.Text;
using System.Text.Json;

// MCP Local Bridge - Connects Claude Desktop (stdio) to Headless IDE MCP Server (HTTP/SSE)
// This bridge acts as a transport adapter, converting between stdio and HTTP transports

var serverUrl = args.Length > 0 ? args[0] : "http://localhost:5000/";

// Ensure URL ends with /
if (!serverUrl.EndsWith('/'))
{
    serverUrl += "/";
}

// Log to stderr to avoid interfering with stdio MCP protocol
var logToStderr = (string message) => Console.Error.WriteLine($"[MCP Bridge] {message}");

logToStderr($"Starting MCP bridge to {serverUrl}");

try
{
    using var httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromMinutes(5)
    };

    // Add default headers for MCP SSE transport
    httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/event-stream");

    // Test server connectivity
    try
    {
        var healthResponse = await httpClient.GetAsync($"{serverUrl.TrimEnd('/')}/health");
        if (healthResponse.IsSuccessStatusCode)
        {
            logToStderr("Server health check passed");
        }
        else
        {
            logToStderr($"Warning: Server health check returned {healthResponse.StatusCode}");
        }
    }
    catch (Exception ex)
    {
        logToStderr($"Warning: Could not connect to server health endpoint: {ex.Message}");
    }

    logToStderr("Bridge ready - listening for MCP messages on stdin");

    // Process stdin messages and forward to HTTP server
    using var stdinReader = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
    using var stdoutWriter = new StreamWriter(Console.OpenStandardOutput(), Encoding.UTF8) { AutoFlush = true };

    string? line;
    while ((line = await stdinReader.ReadLineAsync()) != null)
    {
        if (string.IsNullOrWhiteSpace(line))
            continue;

        try
        {
            logToStderr($"Received message from stdin: {line.Substring(0, Math.Min(100, line.Length))}...");

            // Parse the JSON-RPC message
            var jsonDocument = JsonDocument.Parse(line);
            
            // Forward the message to the HTTP server
            var content = new StringContent(line, Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(serverUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                logToStderr($"Server returned error: {response.StatusCode}");
                
                // Send error response back to client
                var errorResponse = new
                {
                    jsonrpc = "2.0",
                    id = jsonDocument.RootElement.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : (int?)null,
                    error = new
                    {
                        code = -32603,
                        message = $"Server returned {response.StatusCode}"
                    }
                };
                
                await stdoutWriter.WriteLineAsync(JsonSerializer.Serialize(errorResponse));
                continue;
            }

            // Read the response
            var responseBody = await response.Content.ReadAsStringAsync();
            
            logToStderr($"Received response from server ({responseBody.Length} bytes)");
            
            // Parse SSE format if present
            var jsonResponse = ParseSseResponse(responseBody);
            
            if (jsonResponse != null)
            {
                logToStderr($"Extracted JSON from SSE: {jsonResponse.Substring(0, Math.Min(100, jsonResponse.Length))}...");
                await stdoutWriter.WriteLineAsync(jsonResponse);
            }
            else
            {
                // If not SSE format, just forward as-is
                logToStderr("Response is not SSE format, forwarding as-is");
                await stdoutWriter.WriteLineAsync(responseBody);
            }
        }
        catch (JsonException ex)
        {
            logToStderr($"Invalid JSON received: {ex.Message}");
        }
        catch (Exception ex)
        {
            logToStderr($"Error processing message: {ex.Message}");
        }
    }

    logToStderr("stdin closed, shutting down bridge");
}
catch (Exception ex)
{
    logToStderr($"Fatal error: {ex.Message}");
    logToStderr($"Stack trace: {ex.StackTrace}");
    return 1;
}

return 0;

// Parse SSE (Server-Sent Events) format and extract JSON data
static string? ParseSseResponse(string sseData)
{
    if (string.IsNullOrWhiteSpace(sseData))
        return null;

    var lines = sseData.Split('\n');
    
    foreach (var line in lines)
    {
        // SSE format: "data: {json}"
        if (line.StartsWith("data: "))
        {
            return line.Substring(6).Trim();
        }
    }
    
    // If no "data:" line found, check if the entire response is JSON
    if (sseData.TrimStart().StartsWith("{"))
    {
        return sseData.Trim();
    }
    
    return null;
}
