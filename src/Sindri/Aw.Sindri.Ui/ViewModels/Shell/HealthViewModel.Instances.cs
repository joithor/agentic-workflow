using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The readers of instance changes (<see cref="HealthViewModel"/>, continued): what a 'workflow.instance-changed' frame re-reads.</summary>
internal sealed partial class HealthViewModel
{
    private readonly List<Func<Uri, InstanceChange?, CancellationToken, Task<bool>>> _instanceReaders = [];

    /// <summary>
    /// Adds a reader of instance changes: told of a change (a 'workflow.instance-changed' frame, after coalescing) it re-reads what it shows of that
    /// instance and says whether the view caught up with the frame's state (false: still behind, read it again shortly). Told of none (null) it
    /// re-reads everything it shows of instances, which the follower asks for each time the instance stream has opened.
    /// </summary>
    /// <param name="reader">Reads from the API's base URL for a change, or for all.</param>
    internal void FollowInstances(Func<Uri, InstanceChange?, CancellationToken, Task<bool>> reader) => _instanceReaders.Add(reader);

    /// <summary>
    /// Has every instance reader read for a change. The view is read live and can lag the frame, so a reader that says it is still behind is asked
    /// again after <see cref="InstanceBackoff"/>, then twice that, then twice again (<see cref="InstanceRetries"/> times), and then left alone: the
    /// next frame or event reads it. Only the readers still behind are asked again, and none once a newer frame for the instance is waiting.
    /// </summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="change">The frame, or null to re-read all.</param>
    /// <param name="superseded">Says whether a newer frame for the change's instance is waiting: its read is the one that counts, so this change's retries stop. Null: never.</param>
    /// <param name="cancellationToken">Cancels the reads and the waits.</param>
    /// <returns>A task that completes when every reader caught up, a newer frame took over or the retries are used up.</returns>
    internal async Task ReadInstances(Uri url, InstanceChange? change, Func<InstanceChange, bool>? superseded, CancellationToken cancellationToken)
    {
        var behind = _instanceReaders.ToList();
        var wait = InstanceBackoff;
        for (var retry = 0; ; retry++)
        {
            var still = new List<Func<Uri, InstanceChange?, CancellationToken, Task<bool>>>();
            foreach (var reader in behind)
            {
                if (!await reader(url, change, cancellationToken).ConfigureAwait(true))
                {
                    still.Add(reader);
                }
            }

            if (still.Count == 0 || retry == InstanceRetries)
            {
                return;
            }

            behind = still;
            await Task.Delay(wait, clock, cancellationToken).ConfigureAwait(true);
            if (change is not null && superseded?.Invoke(change) == true)
            {
                return;
            }

            wait *= 2;
        }
    }
}
