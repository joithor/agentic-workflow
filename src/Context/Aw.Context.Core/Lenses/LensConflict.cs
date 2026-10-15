using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Lenses;

/// <summary>Two items that disagree. Only 'supersedes' exists in S-4 ('contradicts' comes with S-CF).</summary>
/// <param name="Kind">'supersedes'.</param>
/// <param name="Newer">The superseding decision.</param>
/// <param name="Older">The superseded one.</param>
public sealed record LensConflict(string Kind, NodeKey Newer, NodeKey Older);
