using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Kernel;

namespace Aw.Context.Core.Facade;

/// <summary>
/// The graph reads of the facade (spec Phase 3, stage S-3b): one node, its neighbors, the path between two. They read
/// the current map with its overrides applied; a session sees only the nodes its repo implements and the records and decisions linked to them (spec §3; owner decision 2026-10-15, B), so nothing
/// outside that scope resolves (SND-CONTEXT-003). IContextEngine extends it.
/// </summary>
public interface IContextGraph
{
    /// <summary>One node by key, alias or name.</summary>
    /// <param name="key">A node key, alias or label.</param>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The node, or SND-CONTEXT-001/003/004/020/030/037.</returns>
    public ValueTask<Result<NodeView>> ShowNode(string key, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>The nodes within <paramref name="depth"/> edges of one node.</summary>
    /// <param name="key">A node key, alias or label.</param>
    /// <param name="depth">0 to 4, or null for 1.</param>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The neighborhood, or SND-CONTEXT-001/003/004/006/020/030/037.</returns>
    public ValueTask<Result<NeighborsView>> Neighbors(string key, int? depth, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>The shortest path between two nodes, at most 8 edges.</summary>
    /// <param name="from">A node key, alias or label.</param>
    /// <param name="to">A node key, alias or label.</param>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The path (found or not), or SND-CONTEXT-001/003/004/020/030/037.</returns>
    public ValueTask<Result<PathView>> Path(string from, string to, ContextScope scope, CancellationToken cancellationToken);
}
