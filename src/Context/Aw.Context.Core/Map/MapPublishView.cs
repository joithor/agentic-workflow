using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Map;

/// <summary>The publish state as the caller may see it (what 'context map status', the Health card and a session read): only the caller's repos appear, in every list, and no count of the others.</summary>
/// <param name="Repos">The repos the view covers.</param>
/// <param name="Hold">The hold with its heads cut to <paramref name="Repos"/>, or null when no draft is held.</param>
/// <param name="LatestDraft">The latest draft's number, or null.</param>
/// <param name="Current">The current version's number, or null.</param>
/// <param name="CurrentHeads">The current version's heads, cut to <paramref name="Repos"/>.</param>
public sealed record MapPublishView(IReadOnlyList<ContextRepoName> Repos, MapHold? Hold, int? LatestDraft, int? Current, IReadOnlyList<MapHead> CurrentHeads);
