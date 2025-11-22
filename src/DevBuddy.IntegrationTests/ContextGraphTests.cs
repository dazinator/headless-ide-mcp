using DevBuddy.Server.Data.Models;
using DevBuddy.Server.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using DuckDB.NET.Data;

namespace DevBuddy.IntegrationTests;

public class ContextGraphTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly DomainService _domainService;
    private readonly GraphNodeService _graphNodeService;

    public ContextGraphTests()
    {
        // Use a unique temporary database for each test
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_contextgraph_{Guid.NewGuid()}.duckdb");
        Environment.SetEnvironmentVariable("DUCKDB_PATH", _testDbPath);

        _domainService = new DomainService(NullLogger<DomainService>.Instance);
        _graphNodeService = new GraphNodeService(NullLogger<GraphNodeService>.Instance);
    }

    [Fact]
    public async Task SeedDefaultData_CreatesGeneralDomainAndNodeTypes()
    {
        // Act
        await _domainService.SeedDefaultDataAsync();

        // Assert
        using var connection = new DuckDBConnection($"DataSource={_testDbPath}");
        await connection.OpenAsync();
        
        // Check domain
        using var domainCmd = connection.CreateCommand();
        domainCmd.CommandText = "SELECT Name, Description FROM Domains WHERE Name = 'General'";
        using var domainReader = await domainCmd.ExecuteReaderAsync();
        Assert.True(await domainReader.ReadAsync());
        Assert.Equal("General", domainReader.GetString(0));
        Assert.Equal("Default domain for general purpose nodes", domainReader.GetString(1));

        // Check node types
        using var nodeTypeCmd = connection.CreateCommand();
        nodeTypeCmd.CommandText = "SELECT Name FROM NodeTypes";
        var nodeTypes = new List<string>();
        using var nodeTypeReader = await nodeTypeCmd.ExecuteReaderAsync();
        while (await nodeTypeReader.ReadAsync())
        {
            nodeTypes.Add(nodeTypeReader.GetString(0));
        }
        Assert.Contains("Project", nodeTypes);
        Assert.Contains("Goal", nodeTypes);
        Assert.Contains("Idea", nodeTypes);

        // Check edge types
        using var edgeTypeCmd = connection.CreateCommand();
        edgeTypeCmd.CommandText = "SELECT Name FROM EdgeTypes";
        var edgeTypes = new List<string>();
        using var edgeTypeReader = await edgeTypeCmd.ExecuteReaderAsync();
        while (await edgeTypeReader.ReadAsync())
        {
            edgeTypes.Add(edgeTypeReader.GetString(0));
        }
        Assert.Contains("relates_to", edgeTypes);
        Assert.Contains("depends_on", edgeTypes);
    }

    [Fact]
    public async Task CreateDomain_WithParent_CreatesHierarchy()
    {
        // Arrange
        await _domainService.SeedDefaultDataAsync();
        
        var parentDomain = new Domain
        {
            Name = "Technology",
            Description = "Technology domain"
        };
        await _domainService.CreateDomainAsync(parentDomain);

        var childDomain = new Domain
        {
            Name = "Software",
            Description = "Software development",
            ParentDomainId = parentDomain.Id
        };

        // Act
        await _domainService.CreateDomainAsync(childDomain);

        // Assert
        var domains = await _domainService.GetAllDomainsAsync();
        Assert.Contains(domains, d => d.Name == "Technology" && d.ParentDomainId == null);
        Assert.Contains(domains, d => d.Name == "Software" && d.ParentDomainId == parentDomain.Id);
    }

    [Fact]
    public async Task CreateNode_WithEmbeddedContent_Success()
    {
        // Arrange
        await _domainService.SeedDefaultDataAsync();
        var domains = await _domainService.GetAllDomainsAsync();
        var generalDomain = domains.First(d => d.Name == "General");
        var nodeTypes = await _domainService.GetNodeTypesForDomainAsync(generalDomain.Id);
        var projectNodeType = nodeTypes.First(nt => nt.Name == "Project");

        var node = new Node
        {
            Name = "My Project",
            DomainId = generalDomain.Id,
            NodeTypeId = projectNodeType.Id,
            ContentStorageType = ContentStorageType.Embedded,
            MetadataJson = "{\"status\":\"active\"}"
        };

        // Act
        var created = await _graphNodeService.CreateNodeAsync(node);

        // Assert
        Assert.NotEqual(0, created.Id);
        var retrieved = await _graphNodeService.GetNodeByIdAsync(created.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("My Project", retrieved.Name);
        Assert.Equal(ContentStorageType.Embedded, retrieved.ContentStorageType);
    }

    [Fact]
    public async Task CreateNode_WithExternalContent_Success()
    {
        // Arrange
        await _domainService.SeedDefaultDataAsync();
        var domains = await _domainService.GetAllDomainsAsync();
        var generalDomain = domains.First(d => d.Name == "General");
        var nodeTypes = await _domainService.GetNodeTypesForDomainAsync(generalDomain.Id);
        var ideaNodeType = nodeTypes.First(nt => nt.Name == "Idea");

        var node = new Node
        {
            Name = "External Idea",
            DomainId = generalDomain.Id,
            NodeTypeId = ideaNodeType.Id,
            ContentStorageType = ContentStorageType.External,
            ExternalUri = "https://github.com/user/repo/README.md"
        };

        // Act
        var created = await _graphNodeService.CreateNodeAsync(node);

        // Assert
        Assert.NotEqual(0, created.Id);
        var retrieved = await _graphNodeService.GetNodeByIdAsync(created.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("External Idea", retrieved.Name);
        Assert.Equal(ContentStorageType.External, retrieved.ContentStorageType);
        Assert.Equal("https://github.com/user/repo/README.md", retrieved.ExternalUri);
    }

    [Fact]
    public async Task CreateEdge_BetweenNodes_Success()
    {
        // Arrange
        await _domainService.SeedDefaultDataAsync();
        var domains = await _domainService.GetAllDomainsAsync();
        var generalDomain = domains.First(d => d.Name == "General");
        var nodeTypes = await _domainService.GetNodeTypesForDomainAsync(generalDomain.Id);
        var projectNodeType = nodeTypes.First(nt => nt.Name == "Project");
        var goalNodeType = nodeTypes.First(nt => nt.Name == "Goal");
        var edgeTypes = await _graphNodeService.GetEdgeTypesAsync();
        var relatesTo = edgeTypes.First(et => et.Name == "relates_to");

        var project = await _graphNodeService.CreateNodeAsync(new Node
        {
            Name = "DevBuddy",
            DomainId = generalDomain.Id,
            NodeTypeId = projectNodeType.Id,
            ContentStorageType = ContentStorageType.Embedded
        });

        var goal = await _graphNodeService.CreateNodeAsync(new Node
        {
            Name = "Learn GraphQL",
            DomainId = generalDomain.Id,
            NodeTypeId = goalNodeType.Id,
            ContentStorageType = ContentStorageType.Embedded
        });

        var edge = new Edge
        {
            FromNodeId = project.Id,
            ToNodeId = goal.Id,
            EdgeTypeId = relatesTo.Id
        };

        // Act
        var createdEdge = await _graphNodeService.CreateEdgeAsync(edge);

        // Assert
        Assert.NotEqual(0, createdEdge.Id);
        var edges = await _graphNodeService.GetEdgesAsync();
        Assert.Contains(edges, e => e.FromNodeId == project.Id && e.ToNodeId == goal.Id);
    }

    public void Dispose()
    {
        // Clean up test database
        try
        {
            if (File.Exists(_testDbPath))
                File.Delete(_testDbPath);
            if (File.Exists(_testDbPath + ".wal"))
                File.Delete(_testDbPath + ".wal");
        }
        catch
        {
            // Ignore cleanup errors
        }
        
        Environment.SetEnvironmentVariable("DUCKDB_PATH", null);
    }
}
