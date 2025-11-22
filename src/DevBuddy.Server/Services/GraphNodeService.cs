using DevBuddy.Server.Data.Models;
using DuckDB.NET.Data;

namespace DevBuddy.Server.Services;

public interface IGraphNodeService
{
    Task<List<Node>> GetAllNodesAsync();
    Task<Node?> GetNodeByIdAsync(int id);
    Task<Node> CreateNodeAsync(Node node);
    Task<Node> UpdateNodeAsync(Node node);
    Task DeleteNodeAsync(int id);
    Task<List<Edge>> GetEdgesAsync();
    Task<Edge> CreateEdgeAsync(Edge edge);
    Task DeleteEdgeAsync(int id);
    Task<List<EdgeType>> GetEdgeTypesAsync();
    Task UpdateNodeContentAsync(int nodeId, string content, string contentType = "text/markdown");
}

public class GraphNodeService : IGraphNodeService
{
    private readonly ILogger<GraphNodeService> _logger;
    private readonly string _duckDbPath;

    public GraphNodeService(ILogger<GraphNodeService> logger)
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

    public async Task<List<Node>> GetAllNodesAsync()
    {
        var nodes = new List<Node>();
        
        using var connection = new DuckDBConnection($"DataSource={_duckDbPath}");
        await connection.OpenAsync();
        
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT n.Id, n.DomainId, n.NodeTypeId, n.Name, n.MetadataJson, n.ContentStorageType, n.ExternalUri,
                   d.Name as DomainName, d.Description as DomainDescription,
                   nt.Name as NodeTypeName, nt.Description as NodeTypeDescription
            FROM Nodes n
            INNER JOIN Domains d ON n.DomainId = d.Id
            INNER JOIN NodeTypes nt ON n.NodeTypeId = nt.Id
            ORDER BY n.Name";
        
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var domainId = reader.GetInt32(1);
            var nodeTypeId = reader.GetInt32(2);
            
            nodes.Add(new Node
            {
                Id = reader.GetInt32(0),
                DomainId = domainId,
                NodeTypeId = nodeTypeId,
                Name = reader.GetString(3),
                MetadataJson = reader.IsDBNull(4) ? null : reader.GetString(4),
                ContentStorageType = (ContentStorageType)reader.GetInt32(5),
                ExternalUri = reader.IsDBNull(6) ? null : reader.GetString(6),
                Domain = new Domain
                {
                    Id = domainId,
                    Name = reader.GetString(7),
                    Description = reader.IsDBNull(8) ? null : reader.GetString(8)
                },
                NodeType = new NodeType
                {
                    Id = nodeTypeId,
                    DomainId = domainId,
                    Name = reader.GetString(9),
                    Description = reader.IsDBNull(10) ? null : reader.GetString(10)
                }
            });
        }
        
        return nodes;
    }

    public async Task<Node?> GetNodeByIdAsync(int id)
    {
        using var connection = new DuckDBConnection($"DataSource={_duckDbPath}");
        await connection.OpenAsync();
        
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, DomainId, NodeTypeId, Name, MetadataJson, ContentStorageType, ExternalUri 
            FROM Nodes 
            WHERE Id = $id";
        cmd.Parameters.Add(new DuckDBParameter("id", id));
        
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var node = new Node
            {
                Id = reader.GetInt32(0),
                DomainId = reader.GetInt32(1),
                NodeTypeId = reader.GetInt32(2),
                Name = reader.GetString(3),
                MetadataJson = reader.IsDBNull(4) ? null : reader.GetString(4),
                ContentStorageType = (ContentStorageType)reader.GetInt32(5),
                ExternalUri = reader.IsDBNull(6) ? null : reader.GetString(6)
            };
            
            // Load document if it's embedded content
            if (node.ContentStorageType == ContentStorageType.Embedded)
            {
                using var docCmd = connection.CreateCommand();
                docCmd.CommandText = "SELECT Id, Content, ContentType FROM Documents WHERE NodeId = $nodeId";
                docCmd.Parameters.Add(new DuckDBParameter("nodeId", id));
                
                using var docReader = await docCmd.ExecuteReaderAsync();
                if (await docReader.ReadAsync())
                {
                    node.Document = new Document
                    {
                        Id = docReader.GetInt32(0),
                        NodeId = id,
                        Content = docReader.GetString(1),
                        ContentType = docReader.GetString(2)
                    };
                }
            }
            
            return node;
        }
        
        return null;
    }

    public async Task<Node> CreateNodeAsync(Node node)
    {
        using var connection = new DuckDBConnection($"DataSource={_duckDbPath}");
        await connection.OpenAsync();
        
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Nodes (DomainId, NodeTypeId, Name, MetadataJson, ContentStorageType, ExternalUri) 
            VALUES ($domainId, $nodeTypeId, $name, $metadata, $storageType, $externalUri)
            RETURNING Id";
        cmd.Parameters.Add(new DuckDBParameter("domainId", node.DomainId));
        cmd.Parameters.Add(new DuckDBParameter("nodeTypeId", node.NodeTypeId));
        cmd.Parameters.Add(new DuckDBParameter("name", node.Name));
        cmd.Parameters.Add(new DuckDBParameter("metadata", (object?)node.MetadataJson ?? DBNull.Value));
        cmd.Parameters.Add(new DuckDBParameter("storageType", (int)node.ContentStorageType));
        cmd.Parameters.Add(new DuckDBParameter("externalUri", (object?)node.ExternalUri ?? DBNull.Value));
        
        var id = await cmd.ExecuteScalarAsync();
        node.Id = Convert.ToInt32(id);
        
        return node;
    }

    public async Task<Node> UpdateNodeAsync(Node node)
    {
        using var connection = new DuckDBConnection($"DataSource={_duckDbPath}");
        await connection.OpenAsync();
        
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE Nodes 
            SET DomainId = $domainId, 
                NodeTypeId = $nodeTypeId, 
                Name = $name, 
                MetadataJson = $metadata, 
                ContentStorageType = $storageType, 
                ExternalUri = $externalUri 
            WHERE Id = $id";
        cmd.Parameters.Add(new DuckDBParameter("id", node.Id));
        cmd.Parameters.Add(new DuckDBParameter("domainId", node.DomainId));
        cmd.Parameters.Add(new DuckDBParameter("nodeTypeId", node.NodeTypeId));
        cmd.Parameters.Add(new DuckDBParameter("name", node.Name));
        cmd.Parameters.Add(new DuckDBParameter("metadata", (object?)node.MetadataJson ?? DBNull.Value));
        cmd.Parameters.Add(new DuckDBParameter("storageType", (int)node.ContentStorageType));
        cmd.Parameters.Add(new DuckDBParameter("externalUri", (object?)node.ExternalUri ?? DBNull.Value));
        
        await cmd.ExecuteNonQueryAsync();
        
        return node;
    }

    public async Task DeleteNodeAsync(int id)
    {
        using var connection = new DuckDBConnection($"DataSource={_duckDbPath}");
        await connection.OpenAsync();
        
        // Delete associated document first if exists
        using var docCmd = connection.CreateCommand();
        docCmd.CommandText = "DELETE FROM Documents WHERE NodeId = $id";
        docCmd.Parameters.Add(new DuckDBParameter("id", id));
        await docCmd.ExecuteNonQueryAsync();
        
        // Delete the node
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Nodes WHERE Id = $id";
        cmd.Parameters.Add(new DuckDBParameter("id", id));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<Edge>> GetEdgesAsync()
    {
        var edges = new List<Edge>();
        
        using var connection = new DuckDBConnection($"DataSource={_duckDbPath}");
        await connection.OpenAsync();
        
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, FromNodeId, ToNodeId, EdgeTypeId, MetadataJson FROM Edges";
        
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            edges.Add(new Edge
            {
                Id = reader.GetInt32(0),
                FromNodeId = reader.GetInt32(1),
                ToNodeId = reader.GetInt32(2),
                EdgeTypeId = reader.GetInt32(3),
                MetadataJson = reader.IsDBNull(4) ? null : reader.GetString(4)
            });
        }
        
        return edges;
    }

    public async Task<Edge> CreateEdgeAsync(Edge edge)
    {
        using var connection = new DuckDBConnection($"DataSource={_duckDbPath}");
        await connection.OpenAsync();
        
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Edges (FromNodeId, ToNodeId, EdgeTypeId, MetadataJson) 
            VALUES ($fromNodeId, $toNodeId, $edgeTypeId, $metadata)
            RETURNING Id";
        cmd.Parameters.Add(new DuckDBParameter("fromNodeId", edge.FromNodeId));
        cmd.Parameters.Add(new DuckDBParameter("toNodeId", edge.ToNodeId));
        cmd.Parameters.Add(new DuckDBParameter("edgeTypeId", edge.EdgeTypeId));
        cmd.Parameters.Add(new DuckDBParameter("metadata", (object?)edge.MetadataJson ?? DBNull.Value));
        
        var id = await cmd.ExecuteScalarAsync();
        edge.Id = Convert.ToInt32(id);
        
        return edge;
    }

    public async Task DeleteEdgeAsync(int id)
    {
        using var connection = new DuckDBConnection($"DataSource={_duckDbPath}");
        await connection.OpenAsync();
        
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Edges WHERE Id = $id";
        cmd.Parameters.Add(new DuckDBParameter("id", id));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<EdgeType>> GetEdgeTypesAsync()
    {
        var edgeTypes = new List<EdgeType>();
        
        using var connection = new DuckDBConnection($"DataSource={_duckDbPath}");
        await connection.OpenAsync();
        
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Description FROM EdgeTypes ORDER BY Name";
        
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            edgeTypes.Add(new EdgeType
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Description = reader.IsDBNull(2) ? null : reader.GetString(2)
            });
        }
        
        return edgeTypes;
    }

    public async Task UpdateNodeContentAsync(int nodeId, string content, string contentType = "text/markdown")
    {
        using var connection = new DuckDBConnection($"DataSource={_duckDbPath}");
        await connection.OpenAsync();
        
        // Check if node exists and is embedded type
        using var nodeCmd = connection.CreateCommand();
        nodeCmd.CommandText = "SELECT ContentStorageType FROM Nodes WHERE Id = $id";
        nodeCmd.Parameters.Add(new DuckDBParameter("id", nodeId));
        
        var storageType = await nodeCmd.ExecuteScalarAsync();
        if (storageType == null)
            throw new InvalidOperationException($"Node with ID {nodeId} not found");
        
        if ((ContentStorageType)Convert.ToInt32(storageType) != ContentStorageType.Embedded)
            throw new InvalidOperationException("Cannot update content for non-embedded nodes");
        
        // Check if document exists
        using var checkCmd = connection.CreateCommand();
        checkCmd.CommandText = "SELECT COUNT(*) FROM Documents WHERE NodeId = $nodeId";
        checkCmd.Parameters.Add(new DuckDBParameter("nodeId", nodeId));
        var docExists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;
        
        if (docExists)
        {
            // Update existing document
            using var updateCmd = connection.CreateCommand();
            updateCmd.CommandText = @"
                UPDATE Documents 
                SET Content = $content, ContentType = $contentType 
                WHERE NodeId = $nodeId";
            updateCmd.Parameters.Add(new DuckDBParameter("content", content));
            updateCmd.Parameters.Add(new DuckDBParameter("contentType", contentType));
            updateCmd.Parameters.Add(new DuckDBParameter("nodeId", nodeId));
            await updateCmd.ExecuteNonQueryAsync();
        }
        else
        {
            // Create new document
            using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = @"
                INSERT INTO Documents (NodeId, Content, ContentType) 
                VALUES ($nodeId, $content, $contentType)";
            insertCmd.Parameters.Add(new DuckDBParameter("nodeId", nodeId));
            insertCmd.Parameters.Add(new DuckDBParameter("content", content));
            insertCmd.Parameters.Add(new DuckDBParameter("contentType", contentType));
            await insertCmd.ExecuteNonQueryAsync();
        }
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
