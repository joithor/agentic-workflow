using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Host.Commands;

/// <summary>What the follower has done for one job: the progress rounds already printed, and whether it asked for the cancel.</summary>
/// <param name="started">The started job.</param>
/// <param name="json">Whether it prints the NDJSON form (no progress lines).</param>
internal sealed class JobFollowed(JobStartedResponse started, bool json)
{
    public JobStartedResponse Started { get; } = started;

    public bool Json { get; } = json;

    /// <summary>The highest progress round printed.</summary>
    public int Printed { get; set; }

    /// <summary>When the follower asked to cancel the job, or null.</summary>
    public DateTimeOffset? CancelledAt { get; set; }
}
