using DuckDB.NET.Data;

namespace DevBuddy.Server.Services;

public interface IContextGraphService
{
    Task InitializeAsync();
    Task SyncFromSqliteAsync();
}

public class ContextGraphService : IContextGraphService, IDisposable
{
    private readonly ILogger<ContextGraphService> _logger;
    private readonly string _duckDbPath;
    private DuckDBConnection? _connection;
    private bool _initialized;

    public ContextGraphService(ILogger<ContextGraphService> logger)
    {
        _logger = logger;
        
        // Get the database path from environment variable or use default
        var dbPath = Environment.GetEnvironmentVariable("DUCKDB_PATH");
        if (string.IsNullOrEmpty(dbPath))
        {
            // Check if /data directory exists and is writable, otherwise use temp
            if (Directory.Exists("/data") && IsDirectoryWritable("/data"))
            {
                dbPath = "/data/contextgraph.duckdb";
            }
            else
            {
                dbPath = Path.Combine(Path.GetTempPath(), "contextgraph.duckdb");
            }
        }

        _duckDbPath = dbPath;

        // Ensure the database directory exists
        var dbDirectory = Path.GetDirectoryName(_duckDbPath);
        if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
        {
            Directory.CreateDirectory(dbDirectory);
        }
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
            return;

        try
        {
            _logger.LogInformation("Initializing DuckDB context graph at {Path}", _duckDbPath);

            // Create connection
            _connection = new DuckDBConnection($"DataSource={_duckDbPath}");
            _connection.Open();

            // Install and load DuckPGQ extension
            // Note: DuckPGQ must be available in the DuckDB community repository
            // If this fails, the graph features will be unavailable but the app will still run
            try
            {
                await ExecuteNonQueryAsync("INSTALL duckpgq FROM community");
                await ExecuteNonQueryAsync("LOAD duckpgq");
                _logger.LogInformation("DuckDB context graph with DuckPGQ initialized successfully");
            }
            catch (Exception pgqEx)
            {
                _logger.LogWarning(pgqEx, "DuckPGQ extension could not be loaded - graph features will be limited");
            }

            _initialized = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize DuckDB context graph - will retry later");
            // Don't throw - allow the app to start even if DuckDB initialization fails
        }
    }

    public async Task SyncFromSqliteAsync()
    {
        if (!_initialized || _connection == null)
        {
            throw new InvalidOperationException("ContextGraphService not initialized");
        }

        try
        {
            _logger.LogInformation("Syncing context graph from SQLite");

            // Get SQLite database path
            var sqliteDbPath = Environment.GetEnvironmentVariable("DB_PATH");
            if (string.IsNullOrEmpty(sqliteDbPath))
            {
                if (Directory.Exists("/data") && IsDirectoryWritable("/data"))
                {
                    sqliteDbPath = "/data/devbuddy.db";
                }
                else
                {
                    sqliteDbPath = Path.Combine(Path.GetTempPath(), "devbuddy.db");
                }
            }

            // Attach SQLite database
            await ExecuteNonQueryAsync($"INSTALL sqlite");
            await ExecuteNonQueryAsync($"LOAD sqlite");
            await ExecuteNonQueryAsync($"ATTACH '{sqliteDbPath}' AS sqlite_db (TYPE SQLITE)");

            // Create or replace tables from SQLite
            await ExecuteNonQueryAsync(@"
                CREATE OR REPLACE TABLE Node AS 
                SELECT * FROM sqlite_db.Nodes
            ");

            await ExecuteNonQueryAsync(@"
                CREATE OR REPLACE TABLE Edge AS 
                SELECT * FROM sqlite_db.Edges
            ");

            // Create property graph
            await ExecuteNonQueryAsync(@"
                CREATE OR REPLACE PROPERTY GRAPH context_graph
                VERTEX TABLES (
                    Node PROPERTIES (Id, DomainId, NodeTypeId, Name, MetadataJson, ContentStorageType, ExternalUri)
                )
                EDGE TABLES (
                    Edge 
                        SOURCE KEY (FromNodeId) REFERENCES Node (Id)
                        DESTINATION KEY (ToNodeId) REFERENCES Node (Id)
                        PROPERTIES (EdgeTypeId, MetadataJson)
                )
            ");

            _logger.LogInformation("Context graph synced successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync context graph");
            throw;
        }
    }

    private async Task ExecuteNonQueryAsync(string sql)
    {
        if (_connection == null)
            throw new InvalidOperationException("Connection not initialized");

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        // ExecuteNonQuery is synchronous in DuckDB.NET
        await Task.Run(() => cmd.ExecuteNonQuery());
    }

    private static bool IsDirectoryWritable(string dirPath)
    {
        try
        {
            var testFile = Path.Combine(dirPath, Path.GetRandomFileName());
            using (File.Create(testFile)) { }
            File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
