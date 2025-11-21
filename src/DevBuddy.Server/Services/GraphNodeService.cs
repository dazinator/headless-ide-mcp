using DevBuddy.Server.Data;
using DevBuddy.Server.Data.Models;
using Microsoft.EntityFrameworkCore;

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
    private readonly DevBuddyDbContext _context;
    private readonly ILogger<GraphNodeService> _logger;

    public GraphNodeService(DevBuddyDbContext context, ILogger<GraphNodeService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<Node>> GetAllNodesAsync()
    {
        return await _context.Nodes
            .Include(n => n.Domain)
            .Include(n => n.NodeType)
            .Include(n => n.Document)
            .ToListAsync();
    }

    public async Task<Node?> GetNodeByIdAsync(int id)
    {
        return await _context.Nodes
            .Include(n => n.Domain)
            .Include(n => n.NodeType)
            .Include(n => n.Document)
            .Include(n => n.OutgoingEdges).ThenInclude(e => e.EdgeType)
            .Include(n => n.IncomingEdges).ThenInclude(e => e.EdgeType)
            .FirstOrDefaultAsync(n => n.Id == id);
    }

    public async Task<Node> CreateNodeAsync(Node node)
    {
        _context.Nodes.Add(node);
        await _context.SaveChangesAsync();
        return node;
    }

    public async Task<Node> UpdateNodeAsync(Node node)
    {
        _context.Nodes.Update(node);
        await _context.SaveChangesAsync();
        return node;
    }

    public async Task DeleteNodeAsync(int id)
    {
        var node = await _context.Nodes.FindAsync(id);
        if (node != null)
        {
            _context.Nodes.Remove(node);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<Edge>> GetEdgesAsync()
    {
        return await _context.Edges
            .Include(e => e.FromNode)
            .Include(e => e.ToNode)
            .Include(e => e.EdgeType)
            .ToListAsync();
    }

    public async Task<Edge> CreateEdgeAsync(Edge edge)
    {
        _context.Edges.Add(edge);
        await _context.SaveChangesAsync();
        return edge;
    }

    public async Task DeleteEdgeAsync(int id)
    {
        var edge = await _context.Edges.FindAsync(id);
        if (edge != null)
        {
            _context.Edges.Remove(edge);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<EdgeType>> GetEdgeTypesAsync()
    {
        return await _context.EdgeTypes.ToListAsync();
    }

    public async Task UpdateNodeContentAsync(int nodeId, string content, string contentType = "text/markdown")
    {
        var node = await _context.Nodes
            .Include(n => n.Document)
            .FirstOrDefaultAsync(n => n.Id == nodeId);

        if (node == null)
            throw new InvalidOperationException($"Node with ID {nodeId} not found");

        if (node.ContentStorageType != ContentStorageType.Embedded)
            throw new InvalidOperationException("Cannot update content for non-embedded nodes");

        if (node.Document == null)
        {
            node.Document = new Document
            {
                NodeId = nodeId,
                Content = content,
                ContentType = contentType
            };
            _context.Documents.Add(node.Document);
        }
        else
        {
            node.Document.Content = content;
            node.Document.ContentType = contentType;
        }

        await _context.SaveChangesAsync();
    }
}
