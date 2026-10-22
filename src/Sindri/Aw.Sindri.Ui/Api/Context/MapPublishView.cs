namespace Aw.Sindri.Ui.Api.Context;

/// <summary>GET /sindri/context/map/publish: whether the current map is held for the code index or how it was published (code index spec 6.4).</summary>
/// <param name="State">State, as served: held, overdue, published, published-without-waiting, published-stale or published-by-hand.</param>
/// <param name="Word">Word, as served (the app words the state itself).</param>
/// <param name="Reason">Reason, as served.</param>
/// <param name="Stale">Stale, as served.</param>
/// <param name="StaleRepos">StaleRepos, as served.</param>
/// <param name="Version">Version, as served.</param>
/// <param name="Until">Until, as served.</param>
/// <param name="Lines">Lines, as served (Sindri's sentences; the app shows them as they are).</param>
internal sealed record MapPublishView(
    string State,
    string Word,
    string? Reason,
    bool Stale,
    IReadOnlyList<MapPublishStaleRepo> StaleRepos,
    int Version,
    DateTimeOffset? Until,
    IReadOnlyList<string> Lines);
