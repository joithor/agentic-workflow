namespace Aw.Redaction;

/// <summary>What a scrub did.</summary>
public enum ScrubOutcome
{
    /// <summary>Nothing matched; the text is returned unchanged.</summary>
    Clean,

    /// <summary>At least one secret was replaced by a placeholder.</summary>
    Redacted,

    /// <summary>A pattern timed out; the whole text was withheld (<see cref="RedactionCodes.MatchTimeout"/>).</summary>
    Blocked,
}
