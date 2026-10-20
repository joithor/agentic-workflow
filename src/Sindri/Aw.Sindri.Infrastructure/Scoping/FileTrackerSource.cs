using Aw.Kernel;
using Aw.Sindri.Core.Scoping;

namespace Aw.Sindri.Infrastructure.Scoping;

/// <summary>The tracker port over an exported tracker directory (issues.json and projects.json, the tracker's GraphQL shape).</summary>
/// <param name="directory">The export's directory, or null when no tracker is configured.</param>
internal sealed class FileTrackerSource(string? directory) : ITrackerSource
{
    public ValueTask<Result<TrackerRecord>> Read(string key, TrackerRead read, CancellationToken cancellationToken)
    {
        _ = directory;
        throw new NotSupportedException();
    }
}
