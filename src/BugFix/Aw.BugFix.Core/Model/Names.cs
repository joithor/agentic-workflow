namespace Aw.BugFix.Core.Model;

/// <summary>The names the state file, the CLI and messages use for the enums.</summary>
public static class Names
{
    /// <summary>A phase's name.</summary>
    /// <param name="phase">The phase.</param>
    /// <returns>Its lower-case name.</returns>
    public static string Of(Phase phase) => phase switch
    {
        Phase.Intake => "intake",
        Phase.Investigate => "investigate",
        Phase.Reproduce => "reproduce",
        Phase.Fix => "fix",
        Phase.Evaluate => "evaluate",
        Phase.Report => "report",
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "not a phase"),
    };

    /// <summary>A mode's name.</summary>
    /// <param name="mode">The mode.</param>
    /// <returns>A, B or C.</returns>
    public static string Of(FixMode mode) => mode switch
    {
        FixMode.A => "A",
        FixMode.B => "B",
        FixMode.C => "C",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "not a mode"),
    };
}
