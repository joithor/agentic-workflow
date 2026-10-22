using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Pr;

/// <summary>
/// 'step.pr.snapshot' on the block runtime (babysitter spec §4.3): one forge read of the pull request and its conversation, reported as the first outcome that holds, in the
/// spec's order, with 'fact:prState' on every outcome and 'fact:mergedSha' when merged. The fact is the pull request view in camelCase (link, state, headSha, mergeable,
/// reviewDecision, unresolvedThreads, checks { state, sha, failing }), 'pullState' (open, closed or merged), 'comments' (the threads, oldest first, as the queue Steps parse
/// them), 'self' (null), 'queue' counts, 'mergeBlockedBy' and 'at'. 'state' is 'green' when the outcome is green (the merge gate reads it), else the pull request's own state.
/// Each comment carries its latest mark (outcome, reason, commit, at) from the comment marks, queue.held counts the comments whose latest mark is 'hold'; a marked comment is handled and is not in queue.unhandled or the 'comments' outcome, and 'stale' says the checks ran on another commit than the head.
/// Not read yet, for want of a port: the base branch (restack), the docs pull request (mergedDocsOpen, docsRed) and the workspace.
/// </summary>
/// <param name="pulls">The forge, through Sindri's port.</param>
/// <param name="marks">The comment marks.</param>
/// <param name="diff">Reads the workspace's head.</param>
/// <param name="clock">The clock.</param>
internal sealed class PrSnapshotStep(IPullRequests pulls, IPrCommentMarks marks, IRunDiff diff, TimeProvider clock) : IBlockStep, IBlockStepId
{
    private static readonly string[] _trusted = ["OWNER", "MEMBER", "COLLABORATOR"];

    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.pr.snapshot");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (PrLink.Of(context) is not { } link)
        {
            return BlockStepResult.Failed("step.pr.snapshot needs the pull request's link (fact:pr), which the instance does not hold");
        }

        var pull = await pulls.Get(link, cancellationToken).ConfigureAwait(false);
        if (pull.IsError)
        {
            return BlockStepResult.Failed($"step.pr.snapshot can't read {link}: {pull.Error.Message}");
        }

        var threads = await pulls.Threads(link, cancellationToken).ConfigureAwait(false);
        if (threads.IsError)
        {
            return BlockStepResult.Failed($"step.pr.snapshot can't read the conversation of {link}: {threads.Error.Message}");
        }

        var latest = await LatestMarks(link, cancellationToken).ConfigureAwait(false);
        if (latest.IsError)
        {
            return BlockStepResult.Failed($"step.pr.snapshot can't read the comment marks of {link}: {latest.Error.Message}");
        }

        var work = threads.Value.Where(t => !t.Resolved && !Own(t) && !latest.Value.ContainsKey(t.Key)).ToList();   // a marked comment is handled (spec: a mark not superseded by an edit); the forge port has no login, so Sindri's replies are told apart by their markers alone
        var outcome = Outcome(pull.Value, work.Count);
        var state = State(pull.Value, outcome, threads.Value, work, latest.Value);
        return outcome is "merged"
            ? StepFacts.Done(outcome, ("fact:prState", state), ("fact:mergedSha", JsonValue.Create(pull.Value.HeadSha)!))
            : StepFacts.Done(outcome, ("fact:prState", state));
    }

    // The latest mark per comment across its edits, keyed by the thread key (a mark's CommentId).
    private async ValueTask<Result<Dictionary<string, PrCommentMark>>> LatestMarks(string link, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, PrCommentMark>(StringComparer.Ordinal);
        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri))
        {
            return result;
        }

        var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4 || parts[^2] != "pull" || !int.TryParse(parts[^1], NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return result;
        }

        var rows = await marks.Latest(new PrRef(uri.Host, $"{parts[^4]}/{parts[^3]}", number), cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return rows.Error;
        }

        foreach (var row in rows.Value)
        {
            var key = row.CommentId.Value;
            if (!result.TryGetValue(key, out var seen) || row.At >= seen.At)
            {
                result[key] = row;
            }
        }

        return result;
    }

    private static string Outcome(PullView pull, int unhandled)
    {
        if (pull.State != PullState.Open)
        {
            return pull.State == PullState.Merged ? "merged" : "closed";
        }

        if (pull.Mergeable == false)
        {
            return "conflict";
        }

        var onHead = string.Equals(pull.Checks.Sha, pull.HeadSha, StringComparison.Ordinal);
        if (onHead && pull.Checks.State == PullChecksState.Failing)
        {
            return "ciRed";
        }

        if (unhandled > 0)
        {
            return "comments";
        }

        // Checks on an older commit (right after a push) or still running are not an answer yet; no check at all on the head is a repository without required checks.
        return !onHead || pull.Checks.State == PullChecksState.Pending ? "pending" : "green";
    }

    private JsonObject State(PullView pull, string outcome, IReadOnlyList<PullThread> threads, List<PullThread> work, Dictionary<string, PrCommentMark> marked)
    {
        var pullState = pull.State.ToString().ToLowerInvariant();
        return new JsonObject
        {
            ["link"] = pull.Link,
            ["state"] = outcome is "green" ? "green" : pullState,
            ["pullState"] = pullState,
            ["headSha"] = pull.HeadSha,
            ["stale"] = !string.Equals(pull.Checks.Sha, pull.HeadSha, StringComparison.Ordinal),
            ["head"] = new JsonObject { ["sha"] = pull.HeadSha },
            ["mergeable"] = pull.Mergeable,
            ["reviewDecision"] = pull.ReviewDecision,
            ["unresolvedThreads"] = pull.UnresolvedThreads,
            ["checks"] = new JsonObject
            {
                ["state"] = pull.Checks.State.ToString().ToLowerInvariant(),
                ["sha"] = pull.Checks.Sha,
                ["failing"] = new JsonArray([.. pull.Checks.Failing.Select(static f => (JsonNode)JsonValue.Create(f)!)]),
            },
            ["queue"] = new JsonObject
            {
                ["unhandled"] = work.Count,
                ["held"] = threads.Count(t => !t.Resolved && !Own(t) && marked.TryGetValue(t.Key, out var m) && string.Equals(m.Outcome, "hold", StringComparison.Ordinal)),
                ["untrusted"] = work.Count(static t => !t.IsBot && !_trusted.Contains(t.Association, StringComparer.OrdinalIgnoreCase)),
                ["bots"] = work.Count(static t => t.IsBot),
            },
            ["comments"] = new JsonArray([.. threads.Select(t => (JsonNode)Comment(t, marked.GetValueOrDefault(t.Key)))]),
            ["self"] = null,
            ["mergeBlockedBy"] = new JsonArray([.. BlockedBy(pull).Select(static b => (JsonNode)JsonValue.Create(b)!)]),
            ["heldBlocksMerge"] = false,
            ["at"] = clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture),
        };
    }

    private static IEnumerable<string> BlockedBy(PullView pull)
    {
        if (!string.Equals(pull.ReviewDecision, "APPROVED", StringComparison.Ordinal))
        {
            yield return "reviewRequired";
        }
    }

    private static JsonObject Comment(PullThread thread, PrCommentMark? mark) => new()
    {
        ["key"] = thread.Key,
        ["kind"] = thread.Kind.ToString().ToLowerInvariant(),
        ["author"] = thread.Author,
        ["association"] = thread.Association,
        ["isBot"] = thread.IsBot,
        ["resolved"] = thread.Resolved,
        ["outdated"] = thread.Outdated,
        ["path"] = thread.Path,
        ["body"] = thread.Body,
        ["mark"] = mark is null ? null : new JsonObject
        {
            ["outcome"] = mark.Outcome,
            ["reason"] = mark.Reason,
            ["commit"] = mark.Commit,
            ["at"] = mark.At.ToString("O", CultureInfo.InvariantCulture),
        },
        ["markers"] = new JsonArray([.. thread.Markers.Select(static m => (JsonNode)JsonValue.Create(m)!)]),
    };

    // Sindri's replies never come back as work: a marker it wrote.
    private static bool Own(PullThread thread) =>
        thread.Markers.Any(static m => m.StartsWith("sindri", StringComparison.Ordinal));
}
