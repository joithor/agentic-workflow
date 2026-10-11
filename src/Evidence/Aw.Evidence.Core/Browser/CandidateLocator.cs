using Aw.Evidence.Core.Ids;

namespace Aw.Evidence.Core.Browser;

/// <summary>
/// The interactive element at DOM position <paramref name="Index"/> (as <see cref="Candidate.Index"/> numbered it). An
/// action fails when that element's test id is no longer <paramref name="ExpectedTestId"/>: the DOM shifted.
/// </summary>
/// <param name="Index">The candidate's DOM-order index.</param>
/// <param name="ExpectedTestId">The test id it had when enumerated (null for none).</param>
public sealed record CandidateLocator(int Index, TestId? ExpectedTestId) : Locator;
