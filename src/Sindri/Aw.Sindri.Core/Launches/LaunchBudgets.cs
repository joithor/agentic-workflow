using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Launches;

/// <summary>The daily-budget rule for an open (screens spec E-28, launcher L-5). Pure.</summary>
public static class LaunchBudgets
{
    /// <summary>Where <paramref name="target"/> stands.</summary>
    /// <param name="agents">The approved profile's 'agents:' block, or null.</param>
    /// <param name="target">The agent.</param>
    /// <param name="spentToday">Its spend today in USD, or null when it has no usage reader.</param>
    /// <returns>The state.</returns>
    public static LaunchBudgetState Of(AgentSettings? agents, AgentId target, decimal? spentToday)
    {
        if (agents?.Targets is not { } targets || !targets.TryGetValue(target.Value, out var limits) || limits.DailyBudget is not { } budget)
        {
            return LaunchBudgetState.None;
        }

        if (spentToday is not { } spent)
        {
            return LaunchBudgetState.NoUsageReader;
        }

        var soft = budget * (agents.SoftLimit ?? AgentSettings.DefaultSoftLimit);
        return spent >= budget ? LaunchBudgetState.Hard : spent >= soft ? LaunchBudgetState.Soft : LaunchBudgetState.Ok;
    }
}
