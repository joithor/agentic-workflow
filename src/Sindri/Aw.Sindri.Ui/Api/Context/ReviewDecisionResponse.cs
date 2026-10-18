namespace Aw.Sindri.Ui.Api.Context;

/// <summary>The reply to POST /sindri/context/map/review.</summary>
/// <param name="Result">Result, as served.</param>
/// <param name="Sequence">Sequence, as served.</param>
/// <param name="Challenge">Challenge, as served.</param>
/// <param name="Summary">Summary, as served.</param>
internal sealed record ReviewDecisionResponse(
    ReviewResult? Result,
    long? Sequence,
    ReviewChallenge? Challenge = null,
    string? Summary = null);
