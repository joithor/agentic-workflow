namespace Aw.Context.Core.Concepts;

/// <summary>A concept the owner pinned in 'index.concepts': it takes its members first and keeps its id and name.</summary>
/// <param name="Id">The slug that pins 'concept:&lt;id&gt;', or null to derive it from the name.</param>
/// <param name="Name">The concept's name.</param>
/// <param name="Summary">One sentence, or null.</param>
/// <param name="Features">Feature keys or key globs.</param>
/// <param name="Paths">File globs: the features implemented there.</param>
public sealed record ConceptPin(string? Id, string Name, string? Summary, IReadOnlyList<string> Features, IReadOnlyList<string> Paths);
