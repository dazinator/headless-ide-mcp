using DevBuddy.Server.Data;
using DevBuddy.Server.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace DevBuddy.Server.Services;

public interface IDomainService
{
    Task<List<Domain>> GetAllDomainsAsync();
    Task<Domain?> GetDomainByIdAsync(int id);
    Task<Domain> CreateDomainAsync(Domain domain);
    Task<Domain> UpdateDomainAsync(Domain domain);
    Task DeleteDomainAsync(int id);
    Task<List<NodeType>> GetNodeTypesForDomainAsync(int domainId);
    Task<NodeType> CreateNodeTypeAsync(NodeType nodeType);
    Task DeleteNodeTypeAsync(int id);
    Task SeedDefaultDataAsync();
}

public class DomainService : IDomainService
{
    private readonly DevBuddyDbContext _context;
    private readonly ILogger<DomainService> _logger;

    public DomainService(DevBuddyDbContext context, ILogger<DomainService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<Domain>> GetAllDomainsAsync()
    {
        return await _context.Domains
            .Include(d => d.ParentDomain)
            .Include(d => d.ChildDomains)
            .Include(d => d.NodeTypes)
            .ToListAsync();
    }

    public async Task<Domain?> GetDomainByIdAsync(int id)
    {
        return await _context.Domains
            .Include(d => d.ParentDomain)
            .Include(d => d.ChildDomains)
            .Include(d => d.NodeTypes)
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<Domain> CreateDomainAsync(Domain domain)
    {
        _context.Domains.Add(domain);
        await _context.SaveChangesAsync();
        return domain;
    }

    public async Task<Domain> UpdateDomainAsync(Domain domain)
    {
        _context.Domains.Update(domain);
        await _context.SaveChangesAsync();
        return domain;
    }

    public async Task DeleteDomainAsync(int id)
    {
        var domain = await _context.Domains.FindAsync(id);
        if (domain != null)
        {
            _context.Domains.Remove(domain);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<NodeType>> GetNodeTypesForDomainAsync(int domainId)
    {
        return await _context.NodeTypes
            .Where(nt => nt.DomainId == domainId)
            .ToListAsync();
    }

    public async Task<NodeType> CreateNodeTypeAsync(NodeType nodeType)
    {
        _context.NodeTypes.Add(nodeType);
        await _context.SaveChangesAsync();
        return nodeType;
    }

    public async Task DeleteNodeTypeAsync(int id)
    {
        var nodeType = await _context.NodeTypes.FindAsync(id);
        if (nodeType != null)
        {
            _context.NodeTypes.Remove(nodeType);
            await _context.SaveChangesAsync();
        }
    }

    public async Task SeedDefaultDataAsync()
    {
        try
        {
            // Check if General domain exists
            var generalDomain = await _context.Domains.FirstOrDefaultAsync(d => d.Name == "General");
            if (generalDomain == null)
            {
                _logger.LogInformation("Seeding default domain and node types");

                // Create General domain
                generalDomain = new Domain
                {
                    Name = "General",
                    Description = "Default domain for general purpose nodes"
                };
                _context.Domains.Add(generalDomain);
                await _context.SaveChangesAsync();

                // Create default node types for General domain
                var defaultNodeTypes = new[]
                {
                    new NodeType { DomainId = generalDomain.Id, Name = "Project", Description = "A project or initiative" },
                    new NodeType { DomainId = generalDomain.Id, Name = "Goal", Description = "A goal or objective" },
                    new NodeType { DomainId = generalDomain.Id, Name = "Idea", Description = "An idea or concept" }
                };
                _context.NodeTypes.AddRange(defaultNodeTypes);
                await _context.SaveChangesAsync();

                // Create default edge types
                var edgeTypeExists = await _context.EdgeTypes.AnyAsync();
                if (!edgeTypeExists)
                {
                    var defaultEdgeTypes = new[]
                    {
                        new EdgeType { Name = "relates_to", Description = "Captures adjacency: conceptual, semantic, thematic, contextual, informational" },
                        new EdgeType { Name = "depends_on", Description = "Directional causal or structural requirement" }
                    };
                    _context.EdgeTypes.AddRange(defaultEdgeTypes);
                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation("Default domain and node types seeded successfully");
            }
        }
        catch (Exception ex)
        {
            // If tables don't exist yet, just log and continue
            _logger.LogWarning(ex, "Could not seed default data. Tables may not exist yet.");
        }
    }
}
