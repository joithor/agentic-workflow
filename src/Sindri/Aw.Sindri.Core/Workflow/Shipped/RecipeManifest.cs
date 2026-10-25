using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Shipped;

/// <summary>
/// What <c language="text">sindri recipe manifest</c> prints (setup-once spec §5.3), and what <c language="text">recipe validate --against</c> reads back: the recipes of a set by id, with the digest over them.
/// </summary>
/// <param name="Recipes">Every recipe that read, sorted by id (ordinal).</param>
/// <param name="Invalid">Every recipe file that did not read; empty when the set is sound.</param>
/// <param name="Digest">The SHA-256 over the lines <c language="text">id@version TAB hash TAB evalsHash</c> in <paramref name="Recipes"/> order; null when <paramref name="Invalid"/> is not empty.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RecipeManifest(IReadOnlyList<ManifestEntry> Recipes, IReadOnlyList<ManifestInvalid> Invalid, string? Digest);
