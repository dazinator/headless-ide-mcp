using DevBuddy.Server.Data;
using DevBuddy.Server.Data.Models;
using DevBuddy.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevBuddy.IntegrationTests;

public class ContextGraphTests : IDisposable
{
    private readonly DevBuddyDbContext _context;
    private readonly DomainService _domainService;
    private readonly GraphNodeService _graphNodeService;

    public ContextGraphTests()
    {
        var options = new DbContextOptionsBuilder<DevBuddyDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _context = new DevBuddyDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _domainService = new DomainService(_context, NullLogger<DomainService>.Instance);
        _graphNodeService = new GraphNodeService(_context, NullLogger<GraphNodeService>.Instance);
    }

    [Fact]
    public async Task SeedDefaultData_CreatesGeneralDomainAndNodeTypes()
    {
        // Act
        await _domainService.SeedDefaultDataAsync();

        // Assert
        var generalDomain = await _context.Domains.FirstOrDefaultAsync(d => d.Name == "General");
        Assert.NotNull(generalDomain);
        Assert.Equal("Default domain for general purpose nodes", generalDomain.Description);

        var nodeTypes = await _context.NodeTypes.Where(nt => nt.DomainId == generalDomain.Id).ToListAsync();
        Assert.NotEmpty(nodeTypes);
        Assert.Contains(nodeTypes, nt => nt.Name == "Project");
        Assert.Contains(nodeTypes, nt => nt.Name == "Goal");
        Assert.Contains(nodeTypes, nt => nt.Name == "Idea");

        var edgeTypes = await _context.EdgeTypes.ToListAsync();
        Assert.NotEmpty(edgeTypes);
        Assert.Contains(edgeTypes, et => et.Name == "relates_to");
        Assert.Contains(edgeTypes, et => et.Name == "depends_on");
    }

    [Fact]
    public async Task CreateDomain_WithParent_CreatesHierarchy()
    {
        // Arrange
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
        var generalDomain = await _context.Domains.FirstAsync(d => d.Name == "General");
        var projectNodeType = await _context.NodeTypes.FirstAsync(nt => nt.Name == "Project");

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
        var generalDomain = await _context.Domains.FirstAsync(d => d.Name == "General");
        var ideaNodeType = await _context.NodeTypes.FirstAsync(nt => nt.Name == "Idea");

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
        var generalDomain = await _context.Domains.FirstAsync(d => d.Name == "General");
        var projectNodeType = await _context.NodeTypes.FirstAsync(nt => nt.Name == "Project");
        var goalNodeType = await _context.NodeTypes.FirstAsync(nt => nt.Name == "Goal");
        var relatesTo = await _context.EdgeTypes.FirstAsync(et => et.Name == "relates_to");

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
        _context.Database.CloseConnection();
        _context.Dispose();
    }
}
