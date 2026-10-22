using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.CodeIndex.Features.BuildCodeIndex;

/// <summary>The build slice (spec §5.7): a heavy slot, the snapshot, the incremental plan, the structure write, the edges and the publish.</summary>
public sealed class BuildCodeIndexService : IService<BuildCodeIndexRequest, BuildReport>
{
    private readonly object[] _ports;

    /// <summary>Creates the service over its ports.</summary>
    /// <param name="heads">Whether a commit is still on the default branch.</param>
    /// <param name="snapshot">The repo's tree and blobs at a commit.</param>
    /// <param name="parsers">The parsers, chosen by file extension.</param>
    /// <param name="quarantine">The crash guard around parsing.</param>
    /// <param name="scrubber">Scrubs signatures before they are stored.</param>
    /// <param name="store">The index files.</param>
    /// <param name="heavy">The box-wide heavy-job slots.</param>
    /// <param name="clock">The time source.</param>
    public BuildCodeIndexService(IRepoHeads heads, IRepoSnapshot snapshot, IEnumerable<ISymbolParser> parsers, IParseQuarantine quarantine, IBodyScrubber scrubber, IIndexStore store, IHeavyJobLock heavy, TimeProvider clock)
    {
        _ports = [heads, snapshot, parsers, quarantine, scrubber, store, heavy, clock];
    }

    /// <inheritdoc />
    public ValueTask<Result<BuildReport>> Handle(BuildCodeIndexRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException($"BuildCodeIndex is not built yet ({_ports.Length} ports).");
}
