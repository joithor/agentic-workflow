using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Launches;

/// <summary>Folds the newest 'launch.probed' with the profile's budgets and today's spend into 'GET /launch/targets' (UI spec E-3). Pure.</summary>
public static class LaunchTargetsProjection
{
    /// <summary>Builds the view.</summary>
    /// <param name="probed">The newest probe, or null before the first.</param>
    /// <param name="probedAt">When it was recorded.</param>
    /// <param name="agents">The approved profile's 'agents:' block, or null.</param>
    /// <param name="spend">Today's spend per agent id; an id without an entry has no usage reader.</param>
    /// <param name="asOf">How fresh the views are.</param>
    /// <returns>The view.</returns>
    public static LaunchTargets Project(LaunchProbed? probed, DateTimeOffset? probedAt, AgentSettings? agents, IReadOnlyDictionary<string, AgentSpend> spend, AsOf asOf)
        => throw new NotSupportedException("LaunchTargetsProjection.Project: make the projection tests pass");
}
