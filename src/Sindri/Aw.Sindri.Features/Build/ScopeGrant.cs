using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.Build;

/// <summary>
/// What the owner allowed at the scope desk (line spec §7.2): the last <c language="text">{ outcome: "allow", paths, reason }</c> entry of 'fact:answers', the array 'step.build.prepare' seeds
/// and the desk's 'allow' and 'revert' answers append to. A grant with no path or no reason is no grant.
/// </summary>
/// <param name="Paths">The granted paths: repo-relative globs, or 'new: &lt;path&gt;'.</param>
/// <param name="Reason">Why the owner allowed them.</param>
internal sealed record ScopeGrant(IReadOnlyList<string> Paths, string Reason)
{
    internal static ScopeGrant? Last(JsonNode? answers)
    {
        if (answers is not JsonArray entries)
        {
            return null;
        }

        var allow = entries.OfType<JsonObject>().LastOrDefault(static e => string.Equals(StepFacts.Field(e, "outcome"), "allow", StringComparison.Ordinal));
        var paths = allow?["paths"] is JsonArray listed ? listed.Select(static p => p is JsonValue v && v.TryGetValue<string>(out var text) ? text : null).OfType<string>().Where(static p => !string.IsNullOrWhiteSpace(p)).ToList() : [];
        return paths.Count > 0 && StepFacts.Field(allow, "reason") is { } reason ? new ScopeGrant(paths, reason) : null;
    }
}
