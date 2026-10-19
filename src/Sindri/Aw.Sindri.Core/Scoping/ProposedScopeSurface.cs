namespace Aw.Sindri.Core.Scoping;

/// <summary>A surface of the scope map that has no node key yet, as a run proposes it (scoping spec C9).</summary>
/// <param name="Title">What the run calls it.</param>
/// <param name="Kind">The map node kind the run gave it ('feature', 'surface', 'action', 'thing' or 'flow'), or null when it could not tell.</param>
public sealed record ProposedScopeSurface(string Title, string? Kind);
