namespace Aw.Context.Core.Eval.Backtest;

/// <summary>What a brief source listed.</summary>
/// <param name="Issues">The issues in key order.</param>
/// <param name="CappedTeams">The team keys whose listing hit the source's per-search cap and has no further page to read, so issues past it are not in the corpus (the run says so).</param>
public sealed record BriefListing(IReadOnlyList<BriefIssue> Issues, IReadOnlyList<string> CappedTeams);
