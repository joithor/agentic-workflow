using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The profile's 'workflow:' times (engine spec S1-D11) for the worker: 'workflow.deskTimeout', the floor every desk
/// wait is raised to, and 'workflow.eventHold', how long a buffered event signal waits. 'LoadRecipe' reads it when an
/// instance starts, so the interpreter takes the value from history and a replay sees the same one. A profile that
/// doesn't load gives the defaults (2d, 24h): serve refuses to run on an invalid profile before any instance starts.
/// </summary>
/// <param name="profiles">The live profile.</param>
internal sealed class ProfileWorkflowTimes(IProfileSource profiles)
{
    /// <summary>The times the profile sets, each key's default where it sets none.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The times.</returns>
    public async ValueTask<WorkflowTimes> Read(CancellationToken cancellationToken)
    {
        var loaded = await profiles.Load(cancellationToken).ConfigureAwait(false);
        return loaded.IsError || loaded.IsSuccess ? WorkflowTimes.Defaults : WorkflowTimes.Of(loaded.Value.Profile);   // red
    }
}
