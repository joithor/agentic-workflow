using Aw.CodeIndex.Core.Model;
using Aw.Kernel;

namespace Aw.CodeIndex.Core.Ports;

/// <summary>
/// The graph layer's tool (spec §5.5). An implementation must run graphify only under a sandbox that denies the network
/// and confines writes to its snapshot, check the tool against the pins before every run, treat its output as hostile
/// (size caps, strict parse, only files in the snapshot) and keep only codes and counts of what went wrong.
/// </summary>
public interface IGraphRunner
{
    /// <summary>The lock and tool check a build does, with no work.</summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>Success, or the layer code: SND-CODEIDX-007 (no sandbox), 008 (tool missing or failed), 009 (does not match the pins).</returns>
    public ValueTask<Result<Unit>> Probe(CancellationToken cancellationToken);

    /// <summary>Runs graphify on a snapshot of the commit.</summary>
    /// <param name="request">The files and the caps.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The checked graph, or SND-CODEIDX-007, 008 (also for a graph over a cap) or 009.</returns>
    public ValueTask<Result<GraphSnapshot>> Run(GraphRequest request, CancellationToken cancellationToken);
}
