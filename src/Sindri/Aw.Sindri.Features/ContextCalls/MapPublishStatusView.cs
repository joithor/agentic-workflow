namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// GET /sindri/context/map/publish: how the current map was published, or that it is held for the code index (code index spec 6.4). State is
/// one of the ids held, overdue, published, published-without-waiting, published-stale and published-by-hand; the words are for display.
/// </summary>
/// <param name="State">The state id.</param>
/// <param name="Word">The state as a short phrase.</param>
/// <param name="Reason">Why the map was published (the decision's reason), or null while held and for a version no 'published' event names.</param>
/// <param name="Stale">True when the map's commits and the index's differ for any repo the caller sees.</param>
/// <param name="StaleRepos">Those repos with both commits; empty when <paramref name="Stale"/> is false.</param>
/// <param name="Version">The held map's number, else the current map's.</param>
/// <param name="Until">While held, when the wait ends; otherwise null.</param>
/// <param name="PublishedAt">When the current map was published, or null while held or when no event says.</param>
/// <param name="WaitingOn">While held, each repo the wait is on with its word, such as "web (building)".</param>
/// <param name="Lines">The sentences to show, at most two repos' lines and then "and N more".</param>
internal sealed record MapPublishStatusView(string State, string Word, string? Reason, bool Stale, IReadOnlyList<StaleRepo> StaleRepos, int Version, DateTimeOffset? Until, IReadOnlyList<string> Lines, DateTimeOffset? PublishedAt = null, IReadOnlyList<string>? WaitingOn = null);
