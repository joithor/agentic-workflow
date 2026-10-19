using Aw.Kernel;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Sessions;

/// <summary>Looks up the instance, Step and attempt of the Sindri launches the sessions belong to, so a session row says "on &lt;instance&gt; · &lt;state&gt;" with no extra call.</summary>
public static class SessionLaunchSteps
{
    /// <summary>The step of each launch the sessions name. A launch the 'launches' view does not hold has no entry.</summary>
    /// <param name="launches">The 'launches' view.</param>
    /// <param name="sessions">The sessions about to be served.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The steps by launch id, or the view's read error.</returns>
    public static async ValueTask<Result<IReadOnlyDictionary<LaunchId, LaunchStep>>> Of(ILaunchViews launches, IEnumerable<ObservedSession> sessions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(launches);
        ArgumentNullException.ThrowIfNull(sessions);
        var steps = new Dictionary<LaunchId, LaunchStep>();
        foreach (var id in sessions.Select(static s => s.LaunchId).OfType<LaunchId>().Distinct())
        {
            var row = await launches.Get(id, cancellationToken).ConfigureAwait(false);
            if (row.IsError)
            {
                return row.Error;
            }

            if (row.Value is { } found)
            {
                steps[id] = found.Step;
            }
        }

        return steps;
    }
}
