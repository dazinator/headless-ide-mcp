using DevBuddy.Server.Data.Models;
using DuckDB.NET.Data;

namespace DevBuddy.Server.Services;

public interface IDomainService
{
    Task<List<Domain>> GetAllDomainsAsync();
    Task<Domain?> GetDomainByIdAsync(int id);
    Task<Domain> CreateDomainAsync(Domain domain);
    Task<Domain> UpdateDomainAsync(Domain domain);
    Task DeleteDomainAsync(int id);
    Task<List<NodeType>> GetNodeTypesForDomainAsync(int domainId);
    Task<List<NodeType>> GetAllNodeTypesAsync();
    Task<NodeType> CreateNodeTypeAsync(NodeType nodeType);
    Task<NodeType> UpdateNodeTypeAsync(NodeType nodeType);
    Task DeleteNodeTypeAsync(int id);
    Task SeedDefaultDataAsync();
}

public class DomainService : IDomainService
{
    private readonly ILogger<DomainService> _logger;
    private readonly string _duckDbPath;

    public DomainService(ILogger<DomainService> logger)
    {
        _logger = logger;
        
        // Get the database path
        var dbPath = Environment.GetEnvironmentVariable("DUCKDB_PATH");
        if (string.IsNullOrEmpty(dbPath))
        {
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
    }

    public async Task<List<Domain>> GetAllDomainsAsync()
    {
        var domains = new List<Domain>();
        
        using var connection = new DuckDBConnection($"DataSource={_duckDbPath}");
        await connection.OpenAsync();
        
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Description, ParentDomainId FROM Domains ORDER BY Name";
        
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            domains.Add(new Domain
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                ParentDomainId = reader.IsDBNull(3) ? null : reader.GetInt32(3)
            });
        }
        
        return domains;
    }

    public async Task<Domain?> GetDomainByIdAsync(int id)
    {
        using var connection = new DuckDBConnection($"DataSource={_duckDbPath}");
        await connection.OpenAsync();
        
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Description, ParentDomainId FROM Domains WHERE Id = $id";
        cmd.Parameters.Add(new DuckDBParameter("id", id));
        
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new Domain
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                ParentDomainId = reader.IsDBNull(3) ? null : reader.GetInt32(3)
            };
        }
        
        return null;
    }

    public async Task<Domain> CreateDomainAsync(Domain domain)
    {
        using var connection = new DuckDBConnection($"DataSource={_duckDbPath}");
        await connection.OpenAsync();
        
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Domains (Name, Description, ParentDomainId) 
            VALUES ($name, $description, $parentId)
            RETURNING Id";
        cmd.Parameters.Add(new DuckDBParameter("name", domain.Name));
        cmd.Parameters.Add(new DuckDBParameter("description", (object?)domain.Description ?? DBNull.Value));
        cmd.Parameters.Add(new DuckDBParameter("parentId", (object?)domain.ParentDomainId ?? DBNull.Value));
        
        var id = await cmd.ExecuteScalarAsync();
        domain.Id = Convert.ToInt32(id);
        
        return domain;
    }

    public async Task<Domain> UpdateDomainAsync(Domain domain)
    {
        using var connection = new DuckDBConnection($"DataSource={_duckDbPath}");
        await connection.OpenAsync();
        
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE Domains 
            SET Name = $name, Description = $description, ParentDomainId = $parentId 
            WHERE Id = $id";
        cmd.Parameters.Add(new DuckDBParameter("id", domain.Id));
        cmd.Parameters.Add(new DuckDBParameter("name", domain.Name));
        cmd.Parameters.Add(new DuckDBParameter("description", (object?)domain.Description ?? DBNull.Value));
        cmd.Parameters.Add(new DuckDBParameter("parentId", (object?)domain.ParentDomainId ?? DBNull.Value));
        
        await cmd.ExecuteNonQueryAsync();
        
        return domain;
    }

    public async Task DeleteDomainAsync(int id)
    {
        using var connection = new DuckDBConnection($"DataSource={_duckDbPath}");
        await connection.OpenAsync();
        
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Domains WHERE Id = $id";
        cmd.Parameters.Add(new DuckDBParameter("id", id));
        
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<NodeType>> GetNodeTypesForDomainAsync(int domainId)
    {
        var nodeTypes = new List<NodeType>();
        
        using var connection = new DuckDBConnection($"DataSource={_duckDbPath}");
        await connection.OpenAsync();
        
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, DomainId, Name, Description FROM NodeTypes WHERE DomainId = $domainId ORDER BY Name";
        cmd.Parameters.Add(new DuckDBParameter("domainId", domainId));
        
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            nodeTypes.Add(new NodeType
            {
                Id = reader.GetInt32(0),
                DomainId = reader.GetInt32(1),
                Name = reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3)
            });
        }
        
        return nodeTypes;
    }

    public async Task<List<NodeType>> GetAllNodeTypesAsync()
    {
        var nodeTypes = new List<NodeType>();
        
        using var connection = new DuckDBConnection($"DataSource={_duckDbPath}");
        await connection.OpenAsync();
        
        using var cmd = connection.CreateCommand();
        // No parameters needed for this query - fetching all node types
        cmd.CommandText = "SELECT Id, DomainId, Name, Description FROM NodeTypes ORDER BY DomainId, Name";
        
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            nodeTypes.Add(new NodeType
            {
                Id = reader.GetInt32(0),
                DomainId = reader.GetInt32(1),
                Name = reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3)
            });
        }
        
        return nodeTypes;
    }

    public async Task<NodeType> CreateNodeTypeAsync(NodeType nodeType)
    {
        using var connection = new DuckDBConnection($"DataSource={_duckDbPath}");
        await connection.OpenAsync();
        
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO NodeTypes (DomainId, Name, Description) 
            VALUES ($domainId, $name, $description)
            RETURNING Id";
        cmd.Parameters.Add(new DuckDBParameter("domainId", nodeType.DomainId));
        cmd.Parameters.Add(new DuckDBParameter("name", nodeType.Name));
        cmd.Parameters.Add(new DuckDBParameter("description", (object?)nodeType.Description ?? DBNull.Value));
        
        var id = await cmd.ExecuteScalarAsync();
        nodeType.Id = Convert.ToInt32(id);
        
        return nodeType;
    }

    public async Task<NodeType> UpdateNodeTypeAsync(NodeType nodeType)
    {
        using var connection = new DuckDBConnection($"DataSource={_duckDbPath}");
        await connection.OpenAsync();
        
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE NodeTypes 
            SET DomainId = $domainId, Name = $name, Description = $description 
            WHERE Id = $id";
        cmd.Parameters.Add(new DuckDBParameter("id", nodeType.Id));
        cmd.Parameters.Add(new DuckDBParameter("domainId", nodeType.DomainId));
        cmd.Parameters.Add(new DuckDBParameter("name", nodeType.Name));
        cmd.Parameters.Add(new DuckDBParameter("description", (object?)nodeType.Description ?? DBNull.Value));
        
        await cmd.ExecuteNonQueryAsync();
        
        return nodeType;
    }

    public async Task DeleteNodeTypeAsync(int id)
    {
        using var connection = new DuckDBConnection($"DataSource={_duckDbPath}");
        await connection.OpenAsync();
        
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM NodeTypes WHERE Id = $id";
        cmd.Parameters.Add(new DuckDBParameter("id", id));
        
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SeedDefaultDataAsync()
    {
        try
        {
            using var connection = new DuckDBConnection($"DataSource={_duckDbPath}");
            await connection.OpenAsync();
            
            // Create tables if they don't exist
            await CreateTablesIfNeededAsync(connection);
            
            // Check if General domain exists
            using var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = "SELECT COUNT(*) FROM Domains WHERE Name = 'General'";
            var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
            
            if (count == 0)
            {
                _logger.LogInformation("Seeding default domain and node types in DuckDB");

                // Create General domain
                using var insertDomainCmd = connection.CreateCommand();
                insertDomainCmd.CommandText = @"
                    INSERT INTO Domains (Name, Description, ParentDomainId) 
                    VALUES ('General', 'Default domain for general purpose nodes', NULL)
                    RETURNING Id";
                var domainId = Convert.ToInt32(await insertDomainCmd.ExecuteScalarAsync());

                // Create default node types
                var nodeTypes = new[]
                {
                    ("Project", "A project or initiative"),
                    ("Goal", "A goal or objective"),
                    ("Idea", "An idea or concept")
                };

                foreach (var (name, description) in nodeTypes)
                {
                    using var insertNodeTypeCmd = connection.CreateCommand();
                    insertNodeTypeCmd.CommandText = @"
                        INSERT INTO NodeTypes (DomainId, Name, Description) 
                        VALUES ($domainId, $name, $description)";
                    insertNodeTypeCmd.Parameters.Add(new DuckDBParameter("domainId", domainId));
                    insertNodeTypeCmd.Parameters.Add(new DuckDBParameter("name", name));
                    insertNodeTypeCmd.Parameters.Add(new DuckDBParameter("description", description));
                    await insertNodeTypeCmd.ExecuteNonQueryAsync();
                }

                // Create default edge types
                using var checkEdgeTypesCmd = connection.CreateCommand();
                checkEdgeTypesCmd.CommandText = "SELECT COUNT(*) FROM EdgeTypes";
                var edgeTypeCount = Convert.ToInt32(await checkEdgeTypesCmd.ExecuteScalarAsync());
                
                if (edgeTypeCount == 0)
                {
                    var edgeTypes = new[]
                    {
                        ("relates_to", "Captures adjacency: conceptual, semantic, thematic, contextual, informational"),
                        ("depends_on", "Directional causal or structural requirement")
                    };

                    foreach (var (name, description) in edgeTypes)
                    {
                        using var insertEdgeTypeCmd = connection.CreateCommand();
                        insertEdgeTypeCmd.CommandText = @"
                            INSERT INTO EdgeTypes (Name, Description) 
                            VALUES ($name, $description)";
                        insertEdgeTypeCmd.Parameters.Add(new DuckDBParameter("name", name));
                        insertEdgeTypeCmd.Parameters.Add(new DuckDBParameter("description", description));
                        await insertEdgeTypeCmd.ExecuteNonQueryAsync();
                    }
                }

                _logger.LogInformation("Default domain and node types seeded successfully in DuckDB");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not seed default data in DuckDB");
        }
    }

    private async Task CreateTablesIfNeededAsync(DuckDBConnection connection)
    {
        // Create sequence for Domains
        using var seqCmd1 = connection.CreateCommand();
        seqCmd1.CommandText = @"CREATE SEQUENCE IF NOT EXISTS domains_id_seq START 1";
        await Task.Run(() => seqCmd1.ExecuteNonQuery());

        // Create Domains table
        using var cmd1 = connection.CreateCommand();
        cmd1.CommandText = @"
            CREATE TABLE IF NOT EXISTS Domains (
                Id INTEGER PRIMARY KEY DEFAULT nextval('domains_id_seq'),
                Name VARCHAR(200) NOT NULL,
                Description VARCHAR(1000),
                ParentDomainId INTEGER,
                FOREIGN KEY (ParentDomainId) REFERENCES Domains(Id)
            )";
        await Task.Run(() => cmd1.ExecuteNonQuery());

        // Create sequence for NodeTypes
        using var seqCmd2 = connection.CreateCommand();
        seqCmd2.CommandText = @"CREATE SEQUENCE IF NOT EXISTS nodetypes_id_seq START 1";
        await Task.Run(() => seqCmd2.ExecuteNonQuery());

        // Create NodeTypes table
        using var cmd2 = connection.CreateCommand();
        cmd2.CommandText = @"
            CREATE TABLE IF NOT EXISTS NodeTypes (
                Id INTEGER PRIMARY KEY DEFAULT nextval('nodetypes_id_seq'),
                DomainId INTEGER NOT NULL,
                Name VARCHAR(200) NOT NULL,
                Description VARCHAR(1000),
                FOREIGN KEY (DomainId) REFERENCES Domains(Id)
            )";
        await Task.Run(() => cmd2.ExecuteNonQuery());

        // Create sequence for Nodes
        using var seqCmd3 = connection.CreateCommand();
        seqCmd3.CommandText = @"CREATE SEQUENCE IF NOT EXISTS nodes_id_seq START 1";
        await Task.Run(() => seqCmd3.ExecuteNonQuery());

        // Create Nodes table
        using var cmd3 = connection.CreateCommand();
        cmd3.CommandText = @"
            CREATE TABLE IF NOT EXISTS Nodes (
                Id INTEGER PRIMARY KEY DEFAULT nextval('nodes_id_seq'),
                DomainId INTEGER NOT NULL,
                NodeTypeId INTEGER NOT NULL,
                Name VARCHAR(200) NOT NULL,
                MetadataJson VARCHAR,
                ContentStorageType INTEGER NOT NULL,
                ExternalUri VARCHAR(1000),
                FOREIGN KEY (DomainId) REFERENCES Domains(Id),
                FOREIGN KEY (NodeTypeId) REFERENCES NodeTypes(Id)
            )";
        await Task.Run(() => cmd3.ExecuteNonQuery());

        // Create sequence for Documents
        using var seqCmd4 = connection.CreateCommand();
        seqCmd4.CommandText = @"CREATE SEQUENCE IF NOT EXISTS documents_id_seq START 1";
        await Task.Run(() => seqCmd4.ExecuteNonQuery());

        // Create Documents table
        using var cmd4 = connection.CreateCommand();
        cmd4.CommandText = @"
            CREATE TABLE IF NOT EXISTS Documents (
                Id INTEGER PRIMARY KEY DEFAULT nextval('documents_id_seq'),
                NodeId INTEGER NOT NULL,
                Content VARCHAR NOT NULL,
                ContentType VARCHAR(100) NOT NULL,
                FOREIGN KEY (NodeId) REFERENCES Nodes(Id)
            )";
        await Task.Run(() => cmd4.ExecuteNonQuery());

        // Create sequence for EdgeTypes
        using var seqCmd5 = connection.CreateCommand();
        seqCmd5.CommandText = @"CREATE SEQUENCE IF NOT EXISTS edgetypes_id_seq START 1";
        await Task.Run(() => seqCmd5.ExecuteNonQuery());

        // Create EdgeTypes table
        using var cmd5 = connection.CreateCommand();
        cmd5.CommandText = @"
            CREATE TABLE IF NOT EXISTS EdgeTypes (
                Id INTEGER PRIMARY KEY DEFAULT nextval('edgetypes_id_seq'),
                Name VARCHAR(200) NOT NULL,
                Description VARCHAR(1000)
            )";
        await Task.Run(() => cmd5.ExecuteNonQuery());

        // Create sequence for Edges
        using var seqCmd6 = connection.CreateCommand();
        seqCmd6.CommandText = @"CREATE SEQUENCE IF NOT EXISTS edges_id_seq START 1";
        await Task.Run(() => seqCmd6.ExecuteNonQuery());

        // Create Edges table
        using var cmd6 = connection.CreateCommand();
        cmd6.CommandText = @"
            CREATE TABLE IF NOT EXISTS Edges (
                Id INTEGER PRIMARY KEY DEFAULT nextval('edges_id_seq'),
                FromNodeId INTEGER NOT NULL,
                ToNodeId INTEGER NOT NULL,
                EdgeTypeId INTEGER NOT NULL,
                MetadataJson VARCHAR,
                FOREIGN KEY (FromNodeId) REFERENCES Nodes(Id),
                FOREIGN KEY (ToNodeId) REFERENCES Nodes(Id),
                FOREIGN KEY (EdgeTypeId) REFERENCES EdgeTypes(Id)
            )";
        await Task.Run(() => cmd6.ExecuteNonQuery());
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
}
