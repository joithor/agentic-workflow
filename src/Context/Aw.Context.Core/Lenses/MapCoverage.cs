namespace Aw.Context.Core.Lenses;

/// <summary>How well the map covers the files behind an answer (scoping spec C13): the repo's files under the directories of the files that implement the anchors' scope, counted as 'context filemap' counts them.</summary>
/// <param name="Paths">Those directories ('src/layout/'), at most 50.</param>
/// <param name="Truncated">True when more than 50 directories were cut.</param>
/// <param name="Files">Tracked files under them.</param>
/// <param name="Unmapped">Of those, files no 'implements' edge reaches.</param>
/// <param name="Suggested">Of those, files only an unconfirmed edge reaches.</param>
/// <param name="Created">When the map version read was written; this, not the answer's 'asOf', is the stop's moment for a version read.</param>
/// <param name="AgeDays">Whole days from <paramref name="Created"/> to the answer's 'asOf'.</param>
public sealed record MapCoverage(IReadOnlyList<string> Paths, bool Truncated, int Files, int Unmapped, int Suggested, DateTimeOffset Created, int AgeDays);
