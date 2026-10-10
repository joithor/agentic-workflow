using Aw.Packets.Core.Values;

namespace Aw.Packets.Tests.Ids;

/// <summary>Wraps a RepoSlug so the JSON tests can send a missing or null property.</summary>
public sealed record RepoSlugHolder(RepoSlug Id);
