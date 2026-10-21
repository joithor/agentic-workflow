using Aw.Kernel;

namespace Aw.Sindri.Core.Scoping;

/// <summary>The tracker port 'read-tracker' reads through (scoping spec §4, C8): never one tracker hard-wired, one adapter per configured source.</summary>
public interface ITrackerSource
{
    /// <summary>Reads the project or issue the key names.</summary>
    /// <param name="key">An issue key or a project name.</param>
    /// <param name="read">The caps.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The record; SND-SCOPING-005 when no tracker source is configured; an invalid-input error when the key names nothing.</returns>
    public ValueTask<Result<TrackerRecord>> Read(string key, TrackerRead read, CancellationToken cancellationToken);
}
