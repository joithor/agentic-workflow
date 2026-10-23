using Aw.Context.Core.Map;

namespace Aw.Context.Core.WholeMap;

/// <summary>The whole-map question: how much to send, which keys to place, which map to read, and whose repo.</summary>
/// <param name="Detail">'auto', 'nodes' or 'areas' as a transport sent it, or null for auto (anything else is SND-CONTEXT-006).</param>
/// <param name="Locate">Node keys whose area the answer names (up to <see cref="WholeMapRules.MaxLocate"/>).</param>
/// <param name="Read">Which map: the current version, the draft or a retained version.</param>
/// <param name="Repo">A human caller's repo when the profile has more than one, else null (a session is always its own repo).</param>
public sealed record WholeMapQuery(string? Detail, IReadOnlyList<string> Locate, MapRead Read, string? Repo = null);
