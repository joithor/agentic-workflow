using Aw.Kernel;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>The in-memory ISessionDirectory: the one fake, kept honest by SessionDirectoryContract.</summary>
public sealed class InMemorySessionDirectory : ISessionDirectory
{
    private readonly List<LiveSession> _live = [];

    /// <summary>Adds a session whose process is alive.</summary>
    /// <param name="session">The session.</param>
    public void Add(LiveSession session) => _live.Add(session);

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<LiveSession>>> Live(CancellationToken cancellationToken) =>
        Done.Of<IReadOnlyList<LiveSession>>([.. _live]);
}
