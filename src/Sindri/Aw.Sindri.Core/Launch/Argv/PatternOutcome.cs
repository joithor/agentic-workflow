namespace Aw.Sindri.Core.Launch.Argv;

/// <summary>How a session id pattern judged an id. A timeout is its own outcome, so a slow match is never read as a mismatch.</summary>
public enum PatternOutcome
{
    /// <summary>The whole id matched, or there is no pattern.</summary>
    Matched,

    /// <summary>The pattern doesn't match the id, or it doesn't compile (it matches nothing).</summary>
    NoMatch,

    /// <summary>The match ran past its time limit; nothing is known about the id.</summary>
    TimedOut,
}
