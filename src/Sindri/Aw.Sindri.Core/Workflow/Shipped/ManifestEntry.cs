using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Shipped;

/// <summary>One recipe of a manifest (setup-once spec §5.3): <c language="text">id@version → hash</c>, its name and its eval cases.</summary>
/// <param name="Id">The recipe id.</param>
/// <param name="Version">The recipe's version.</param>
/// <param name="Hash">The recipe hash the registry computes ('sha256:' and 64 hex digits).</param>
/// <param name="DisplayName">The recipe's display name; null when the file has none; the recipe is then also listed as invalid (RCP-DISPLAY-001), so the manifest has no digest.</param>
/// <param name="EvalCases">How many <c language="text">evals/&lt;id&gt;/*.jsonl</c> files the set holds for it.</param>
/// <param name="EvalsHash">The SHA-256 over those files (name and bytes, by name), so an eval-only change changes the manifest.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ManifestEntry(RecipeId Id, int Version, string Hash, string? DisplayName, int EvalCases, string EvalsHash);
