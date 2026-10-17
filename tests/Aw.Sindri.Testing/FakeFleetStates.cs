using Aw.Kernel;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Testing;

/// <summary>The aw:fleet hook's state files in memory; records what a prune asked for.</summary>
public sealed class FakeFleetStates : IFleetStates
{
    private readonly List<FleetStateFile> _files = [];
    private readonly List<IReadOnlyCollection<SessionRef>> _prunes = [];

    /// <summary>The live sets the collector passed to <see cref="Prune"/>.</summary>
    public IReadOnlyList<IReadOnlyCollection<SessionRef>> Prunes => _prunes;

    /// <summary>Adds a state file.</summary>
    /// <param name="file">The file.</param>
    /// <returns>These states.</returns>
    public FakeFleetStates With(FleetStateFile file)
    {
        _files.Add(file);
        return this;
    }

    /// <inheritdoc/>
    public Result<IReadOnlyList<FleetStateFile>> Read(CancellationToken cancellationToken) => Results.Success<IReadOnlyList<FleetStateFile>>([.. _files]);

    /// <inheritdoc/>
    public int Prune(IReadOnlyCollection<SessionRef> live, DateTimeOffset now)
    {
        _prunes.Add(live);
        return 0;
    }
}
