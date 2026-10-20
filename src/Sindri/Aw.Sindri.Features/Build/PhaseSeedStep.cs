using System.Text.Json.Nodes;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Worktrees;

namespace Aw.Sindri.Features.Build;

/// <summary>
/// 'step.phase.seed' on the block runtime (line spec §7.1): creates the phase's branch <c language="text">sindri/&lt;line&gt;/&lt;phase&gt;</c> and its worktree at the base the line chose, and seeds the round-1 value of every fact a later wait reads. A retried run (a worker crash, a Temporal retry) asks `Create` again, which is idempotent over the same path, branch and base, so it returns the same 'fact:workspace'. SND-WT-002 (the path or branch is in use at another base) stops the Step with
/// nothing reset, so the instance parks for the owner; SND-WT-003 (git failed) fails it.
/// </summary>
/// <param name="worktrees">Makes the worktree.</param>
/// <param name="checkouts">Where the repo is checked out.</param>
internal sealed class PhaseSeedStep(IWorktrees worktrees, IRepoCheckouts checkouts) : IBlockStep, IBlockStepId, IRepoStep
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.phase.seed");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (StepFacts.Missing(context, "fact:phasePacket", "fact:lineContext") is { Count: > 0 } missing)
        {
            return BlockStepResult.Failed($"step.phase.seed needs {string.Join(", ", missing)}, which the instance does not hold");
        }

        var (request, failure) = await Request(context, cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            return BlockStepResult.Failed(failure ?? "step.phase.seed can't place the worktree");
        }

        var made = await worktrees.Create(request, cancellationToken).ConfigureAwait(false);
        return made.IsError ? BlockStepResult.Failed(Describe(made.Error)) : Seeded(made.Value.Path);
    }

    // SND-WT-002 is for the owner (nothing was reset); any other refusal of the port is a failure of the Step.
    private static string Describe(Kernel.Error error) => string.Equals(error.Code, SindriErrors.WorktreeConflict.Code, StringComparison.Ordinal)
        ? $"step.phase.seed parked: {error.Code} {error.Message}; nothing was reset or moved, so fix the checkout and resume"
        : $"step.phase.seed failed: {error.Code} {error.Message}";

    private static BlockStepResult Seeded(string workspace) => StepFacts.Done(
        "seeded",
        ("fact:buildNotes", new JsonObject()),
        ("fact:workspace", JsonValue.Create(workspace)),
        ("fact:built", new JsonObject()),
        ("fact:change", new JsonObject()),
        ("fact:redTests", new JsonObject()));

    private async ValueTask<(WorktreeRequest? Value, string? Failure)> Request(StepContext context, CancellationToken cancellationToken)
    {
        var (line, failure) = Line(StepFacts.Read(context, "fact:lineContext") as JsonObject);
        if (line is null)
        {
            return (null, failure);
        }

        if (context.Repo is not { } repo)
        {
            return (null, "step.phase.seed needs the instance's repo to place the worktree");
        }

        var checkout = await checkouts.Of(repo, cancellationToken).ConfigureAwait(false);
        return checkout.IsError
            ? (null, $"step.phase.seed has no checkout of {repo.Value} to make the worktree in: {checkout.Error.Message}")
            : (new WorktreeRequest(checkout.Value, line.Base, $"sindri/{line.Name}/{line.Phase}", System.IO.Path.Combine(checkout.Value, ".claude", "worktrees", $"sindri-{line.Name}-{line.Phase}")), null);
    }

    private sealed record LineRef(string Name, string Phase, string Base);

    private static (LineRef? Line, string? Failure) Line(JsonObject? context)
    {
        var name = Segment(context, "line");
        var phase = Segment(context, "phase");
        if (name is null || phase is null)
        {
            return (null, $"step.phase.seed needs fact:lineContext with a {(name is null ? "line" : "phase")} (letters, digits, '.', '_' and '-')");
        }

        var baseCommit = context?["base"] is JsonValue b && b.TryGetValue<string>(out var sha) ? sha : null;
        return baseCommit is { Length: 40 } && baseCommit.All(static c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'))
            ? (new LineRef(name, phase, baseCommit), null)
            : (null, "step.phase.seed needs fact:lineContext with a base that is a full 40-character commit sha");
    }

    // A line or phase id is part of a branch and a directory name, so only plain characters pass.
    private static string? Segment(JsonObject? line, string key) =>
        line?[key] is JsonValue value && value.TryGetValue<string>(out var text) && text.Length > 0 && text.All(static c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-') && text[0] != '.' ? text : null;
}
