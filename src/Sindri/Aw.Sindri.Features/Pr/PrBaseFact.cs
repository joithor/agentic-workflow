using System.Text.Json.Nodes;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.Pr;

/// <summary>
/// The 'base' of 'fact:prState' (babysitter spec §4.3): <c language="text">{ ref, sha, tip, was }</c>. 'ref' is the base branch the forge reports now and 'tip' is its head now. 'sha' is the tip of the base the
/// pull request was built on, which is what 'effect.pr.restack' replays after: while the base is unchanged it is the forge's tip; a base that moved on, or merged and was deleted (the forge then moves the pull request to
/// the default branch, whose tip says nothing about the old base), keeps the last known 'sha', and 'was' names the branch it came from when the branch changed. A restack that settled at this head (<c language="text">fact:restack</c>
/// 'restacked' or 'nothing', its 'head' the pull request's head now) puts the pull request on the current tip: 'sha' becomes 'tip' and 'was' is dropped. A pull request first seen after its base merged has no earlier tip to keep.
/// </summary>
internal static class PrBaseFact
{
    /// <summary>The fact for one snapshot.</summary>
    /// <param name="pull">The pull request as the forge reports it now.</param>
    /// <param name="previous">The previous 'fact:prState', or null.</param>
    /// <param name="restack">The instance's 'fact:restack', or null.</param>
    /// <returns>The 'base' object.</returns>
    public static JsonObject Of(PullView pull, JsonNode? previous, JsonNode? restack)
    {
        ArgumentNullException.ThrowIfNull(pull);
        var before = (previous as JsonObject)?["base"];
        var keptSha = StepFacts.Field(before, "sha");
        var keptRef = StepFacts.Field(before, "ref");
        var settled = Settled(restack, pull.HeadSha);
        var sha = settled ? pull.BaseSha ?? keptSha : keptSha ?? pull.BaseSha;
        var was = settled ? null : keptRef is not null && !string.Equals(keptRef, pull.Base, StringComparison.Ordinal) ? keptRef : StepFacts.Field(before, "was");
        return new JsonObject { ["ref"] = pull.Base, ["sha"] = sha, ["tip"] = pull.BaseSha, ["was"] = was };
    }

    private static bool Settled(JsonNode? restack, string head) =>
        (string.Equals(StepFacts.Field(restack, "outcome"), "restacked", StringComparison.Ordinal) || string.Equals(StepFacts.Field(restack, "outcome"), "nothing", StringComparison.Ordinal))
        && string.Equals(StepFacts.Field(restack, "head"), head, StringComparison.Ordinal);
}
