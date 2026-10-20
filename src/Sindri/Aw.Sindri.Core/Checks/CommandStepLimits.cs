namespace Aw.Sindri.Core.Checks;

/// <summary>How long the Steps that run the repo's commands may run (line spec LN-WF7): the descriptors take their MaxDuration from here and the Steps their command budget, so the two cannot drift apart.</summary>
public static class CommandStepLimits
{
    /// <summary>Gets the longest 'step.build.red-check' and 'step.build.checks' may run.</summary>
    public static TimeSpan BuildMaxDuration { get; } = TimeSpan.FromMinutes(50);

    /// <summary>Gets the longest 'step.ship.green' may run.</summary>
    public static TimeSpan GreenMaxDuration { get; } = TimeSpan.FromMinutes(100);

    /// <summary>Gets the time the slot wait and the commands of a build Step share.</summary>
    public static TimeSpan BuildBudget { get; } = RepoCommandRunner.BudgetFor(BuildMaxDuration);

    /// <summary>Gets the time the slot wait and the commands of 'step.ship.green' share.</summary>
    public static TimeSpan GreenBudget { get; } = RepoCommandRunner.BudgetFor(GreenMaxDuration);
}
