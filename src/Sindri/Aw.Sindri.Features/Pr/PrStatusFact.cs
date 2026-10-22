using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.Pr;

/// <summary>
/// 'fact:prStatus', the merge-stop summary the run view shows, written by 'step.pr.snapshot' on every outcome: pr and url, checks { passed, failed, pending }, review { open, resolved },
/// proof { status } and docs { status }. review.open counts the unresolved threads that are not Sindri's own, handled or not (what the owner sees on the forge; the snapshot's queue.unhandled is the subset still to work).
/// proof is the recheck's verdict ('fact:recheck') on this head, 'held' passed, 'broken' failed, 'nothing' none, and null when no recheck covers the head (the original proof isn't held here); a running recheck is a child instance and no snapshot runs while it does, so 'running' is never written here, and no link is held.
/// docs is 'updated' for a merged or green docs pull request, 'notNeeded' when docs sync found nothing ('none', 'disabled') or the owner skipped it (an owner's skip means they judged no docs needed), else 'missing'.
/// </summary>
internal static class PrStatusFact
{
    private static readonly string[] _noDocsNeeded = ["none", "disabled", "skippedByOwner"];

    /// <summary>The fact.</summary>
    /// <param name="recheck">'fact:recheck', when the instance holds one.</param>
    /// <param name="docsResult">'fact:docsResult', when the instance holds one.</param>
    /// <param name="pull">The pull request.</param>
    /// <param name="threads">Its conversation.</param>
    /// <param name="docs">The docs pull request, when the instance holds one.</param>
    /// <returns>The fact.</returns>
    public static JsonObject Of(JsonNode? recheck, JsonNode? docsResult, PullView pull, IReadOnlyList<PullThread> threads, PullView? docs) => new()
    {
        ["pr"] = Number(pull.Link),
        ["url"] = pull.Link,
        ["checks"] = new JsonObject
        {
            ["passed"] = pull.Checks.Passed,
            ["failed"] = pull.Checks.Failing.Count,
            ["pending"] = pull.Checks.Pending,
        },
        ["review"] = new JsonObject { ["open"] = threads.Count(t => !t.Resolved && !PrSnapshotStep.Own(t)), ["resolved"] = threads.Count(static t => t.Resolved) },
        ["proof"] = Proof(recheck, pull.HeadSha),
        ["docs"] = new JsonObject { ["status"] = Docs(docsResult, docs) },
    };

    // Null when no recheck covers this head: the babysitter then knows nothing of the proof, which is not the same as no proof having run.
    private static JsonObject? Proof(JsonNode? recheck, string headSha) =>
        string.Equals(StepFacts.Field(recheck, "headSha"), headSha, StringComparison.Ordinal)
            ? new JsonObject { ["status"] = Verdict(StepFacts.Field(recheck, "verdict")) }
            : null;

    private static string Verdict(string? verdict) =>
        string.Equals(verdict, "held", StringComparison.Ordinal) ? "passed" : string.Equals(verdict, "broken", StringComparison.Ordinal) ? "failed" : "none";

    private static string Docs(JsonNode? result, PullView? docs)
    {
        if (docs is not null)
        {
            return docs.State == PullState.Merged || docs.Checks.State == PullChecksState.Passing ? "updated" : "missing";
        }

        return StepFacts.Field(result, "result") is { } word && _noDocsNeeded.Contains(word, StringComparer.Ordinal) ? "notNeeded" : "missing";
    }

    // The number in the pull request's link (.../pull/<n>); 0 when it isn't one.
    private static int Number(string link) =>
        Uri.TryCreate(link, UriKind.Absolute, out var uri)
        && uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries) is [.., var kind, var last]
        && string.Equals(kind, "pull", StringComparison.Ordinal)
        && int.TryParse(last, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0;
}
