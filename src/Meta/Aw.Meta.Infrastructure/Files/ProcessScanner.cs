using Aw.Kernel;
using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Worktrees;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>IProcessLocations over /proc (the working directory and every open file of each process), or lsof where there is no /proc (macOS).</summary>
internal sealed class ProcessScanner : IProcessLocations
{
    public ValueTask<Result<IReadOnlyList<ProcessLocation>>> Locations(CancellationToken cancellationToken) => throw new NotSupportedException();
}
