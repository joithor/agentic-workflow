using Aw.Kernel;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>One poll's working state: the clock reading, the settings, the file's records as they stand, what was recorded and the failure that degrades the service.</summary>
internal sealed class PrWatchPoll(ScheduledRun run, DateTimeOffset now, WatchSettings settings, PrWatchFile file)
{
    /// <summary>The scheduler's run: the lease the events are recorded under.</summary>
    public ScheduledRun Run { get; } = run;

    /// <summary>When the poll began.</summary>
    public DateTimeOffset Now { get; } = now;

    /// <summary>The profile's settings.</summary>
    public WatchSettings Settings { get; } = settings;

    /// <summary>The back-off by host.</summary>
    public Dictionary<string, HostBackoff> Hosts { get; } = new(file.Hosts, StringComparer.Ordinal);

    /// <summary>The record of each watched pull request.</summary>
    public Dictionary<string, WatchedRecord> Pulls { get; } = new(file.Pulls, StringComparer.Ordinal);

    /// <summary>How many events were recorded.</summary>
    public int Recorded { get; set; }

    /// <summary>The first error that degrades the service this poll, if any.</summary>
    public Error? Failure { get; set; }
}
