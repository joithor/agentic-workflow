namespace Aw.Meta.Features.Usage;

/// <summary>How a usage wait ended.</summary>
/// <param name="Ok">Whether the window is back; false when the wait ran out.</param>
/// <param name="Lines">The lines to print.</param>
/// <param name="WaitedSeconds">How long it waited.</param>
public sealed record UsageWaitResponse(bool Ok, IReadOnlyList<string> Lines, long WaitedSeconds);
