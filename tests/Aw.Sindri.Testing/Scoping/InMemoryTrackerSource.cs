using Aw.Kernel;
using Aw.Sindri.Core.Scoping;

namespace Aw.Sindri.Testing.Scoping;

/// <summary>The in-memory <see cref="ITrackerSource"/>: the seeded issues and projects, or no tracker at all.</summary>
public sealed class InMemoryTrackerSource : ITrackerSource
{
    /// <summary>A source over the seeded tracker.</summary>
    /// <param name="seed">What the tracker holds.</param>
    public InMemoryTrackerSource(TrackerSeed seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
    }

    private InMemoryTrackerSource()
    {
    }

    /// <summary>A source with no tracker configured.</summary>
    public static InMemoryTrackerSource Unconfigured { get; } = new();

    /// <inheritdoc/>
    public ValueTask<Result<TrackerRecord>> Read(string key, TrackerRead read, CancellationToken cancellationToken) => throw new NotSupportedException();
}
