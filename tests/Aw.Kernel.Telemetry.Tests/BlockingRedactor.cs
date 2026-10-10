using Aw.Redaction;

namespace Aw.Kernel.Telemetry.Tests;

/// <summary>A redactor whose scrub is blocked (as on a match timeout) for any text containing <see cref="Marker"/>.</summary>
internal sealed class BlockingRedactor : IRedactor
{
    public const string Marker = "SLOW-INPUT";

    public bool IsSecretName(string name) => false;

    public ScrubResult Scrub(string text) => text.Contains(Marker, StringComparison.Ordinal)
        ? new ScrubResult(ScrubResult.BlockedText, [], ScrubOutcome.Blocked, RedactionCodes.MatchTimeout)
        : Redactor.Default.Scrub(text);
}
