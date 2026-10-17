namespace Aw.Sindri.Core.Launches;

/// <summary>Where an agent stands against its daily budget (screens spec E-28).</summary>
public enum LaunchBudgetState
{
    /// <summary>The profile sets no budget for the agent.</summary>
    None,

    /// <summary>Under the soft limit.</summary>
    Ok,

    /// <summary>At or over the soft limit (default 80 %) and under the budget: a warning.</summary>
    Soft,

    /// <summary>At or over the budget: opens are refused (SND-LAUNCH-010).</summary>
    Hard,

    /// <summary>A budget is set but the agent has no usage reader, so the spend can't be known: refused (SND-LAUNCH-010).</summary>
    NoUsageReader,
}
