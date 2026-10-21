using System.Text.Json.Nodes;
using Aw.Sindri.Core.BugFix;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.BugFix;

/// <summary>
/// 'step.bugfix.record-candidate' on the block runtime (DT spec 6.1): the implementer's 'fact:diff' (branch and worktree, optionally hypothesis and allowTestChanges) becomes bugfix-state's next candidate.
/// 'fact:candidate' carries what the recheck and the judge need: {"id":"c1","head":sha,"attempt":1,"branch":…,"worktree":…,"changedFiles":[…]}. Any refusal fails the Step with bugfix-state's code and reason.
/// </summary>
/// <param name="state">bugfix-state.</param>
/// <param name="area">The instance's run area, where its bug fix state lives.</param>
internal sealed class RecordCandidateStep(IBugFixState state, IRunArea area) : IBlockStep, IBlockStepId
{
    private const string Name = "step.bugfix.record-candidate";

    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From(Name);

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var diff = StepFacts.Read(context, "fact:diff");
        if (BugFixFacts.Text(diff, "branch") is not { } branch || BugFixFacts.Text(diff, "worktree") is not { } worktree)
        {
            return BlockStepResult.Failed($"{Name} needs fact:diff to name the implementer's branch and worktree");
        }

        var (directory, failure) = BugFixFacts.StateDirectory(Name, context, area);
        if (failure is not null)
        {
            return failure;
        }

        var source = new CandidateSource(branch, worktree, BugFixFacts.Int(diff, "hypothesis"), BugFixFacts.Flag(diff, "allowTestChanges"));
        var recorded = await state.RecordCandidate(directory, source, cancellationToken).ConfigureAwait(false);
        return recorded.Status == BugFixAnswerStatus.Ok && recorded.Value is { } candidate
            ? StepFacts.Done("recorded", ("fact:candidate", new JsonObject
            {
                ["id"] = candidate.Candidate,
                ["head"] = candidate.Commit,
                ["attempt"] = candidate.Attempt,
                ["branch"] = branch,
                ["worktree"] = worktree,
                ["changedFiles"] = new JsonArray([.. candidate.ChangedFiles.Select(static f => (JsonNode?)JsonValue.Create(f))]),
            }))
            : BugFixFacts.Failure(Name, recorded);
    }
}
