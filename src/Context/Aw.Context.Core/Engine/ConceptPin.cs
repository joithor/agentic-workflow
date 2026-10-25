namespace Aw.Context.Core.Engine;

/// <summary>A concept the owner pinned in the profile's 'index.concepts' (context concepts note §3): it takes its members first, and derived concepts fill the rest.</summary>
/// <param name="Id">The slug of its key ('scheduling' pins concept:scheduling), or null to derive one from the name.</param>
/// <param name="Name">What the map shows.</param>
/// <param name="Summary">A line about it, or null.</param>
/// <param name="Features">Feature node keys or key globs ('feature:shifts', 'feature:shifts.*') it holds.</param>
/// <param name="Paths">Repo-relative file globs; the features implemented there belong to it.</param>
public sealed record ConceptPin(ConceptId? Id, string Name, string? Summary, IReadOnlyList<string> Features, IReadOnlyList<string> Paths);
