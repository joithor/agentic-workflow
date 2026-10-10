namespace Aw.Redaction;

/// <summary>The result of <see cref="IRedactor.Scrub"/>.</summary>
/// <param name="Text">The scrubbed text; for <see cref="ScrubOutcome.Blocked"/>, only <see cref="BlockedText"/>.</param>
/// <param name="Hits">The redacted spans in input order, merged where patterns overlapped.</param>
/// <param name="Outcome">Clean, Redacted or Blocked.</param>
/// <param name="BlockCode">The reason code when blocked (<see cref="RedactionCodes.MatchTimeout"/>); otherwise null.</param>
public sealed record ScrubResult(string Text, IReadOnlyList<RedactionHit> Hits, ScrubOutcome Outcome, string? BlockCode)
{
    /// <summary>The text a blocked scrub returns in place of the input.</summary>
    public const string BlockedText = "[REDACTED:blocked]";

    /// <summary>True when the text was withheld.</summary>
    public bool IsBlocked => Outcome == ScrubOutcome.Blocked;
}
