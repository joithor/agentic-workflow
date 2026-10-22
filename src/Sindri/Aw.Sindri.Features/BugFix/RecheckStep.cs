using System.Text.Json.Nodes;
using Aw.Sindri.Core.BugFix;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.BugFix;

/// <summary>
/// 'step.bugfix.recheck' on the block runtime (DT spec 6.1): runs the frozen reproduction check ('fact:check') on the candidate's worktree through bugfix-state, which records the run for the candidate.
/// 'passed' or 'failed' with 'fact:recheck' on both: {"candidate":"c1","passed":true,"exitCode":0,"head":sha,"evidence":run record path}. A refusal (a dirty tree, a changed check, an unknown candidate) fails the Step.
/// </summary>
/// <param name="state">bugfix-state.</param>
/// <param name="area">The instance's run area, where its bug fix state lives.</param>
internal sealed class RecheckStep(IBugFixState state, IRunArea area) : IBlockStep, IBlockStepId
{
    private const string Name = "step.bugfix.recheck";

    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From(Name);

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (FrozenCheckOf(StepFacts.Read(context, "fact:check")) is not { } check)
        {
            return BlockStepResult.Failed($"{Name} needs fact:check to be the frozen check: a 'test' check with a file and a command, or a 'ui' check with a file");
        }

        var candidate = StepFacts.Read(context, "fact:candidate");
        if (BugFixFacts.Candidate(candidate) is not ({ } id, { } worktree))
        {
            return BlockStepResult.Failed($"{Name} needs fact:candidate to carry the candidate's id and worktree");
        }

        var (directory, failure) = BugFixFacts.StateDirectory(Name, context, area);
        if (failure is not null)
        {
            return failure;
        }

        var ran = await state.Recheck(directory, id, worktree, check, cancellationToken).ConfigureAwait(false);
        return ran.Status == BugFixAnswerStatus.Ok && ran.Value is { } run
            ? StepFacts.Done(run.Passed ? "passed" : "failed", ("fact:recheck", new JsonObject
            {
                ["candidate"] = run.Candidate,
                ["passed"] = run.Passed,
                ["exitCode"] = run.ExitCode,
                ["head"] = BugFixFacts.Text(candidate, "head"),
                ["evidence"] = run.Evidence,
            }))
            : BugFixFacts.Failure(Name, ran);
    }

    private static FrozenCheck? FrozenCheckOf(JsonNode? fact)
    {
        var kind = BugFixFacts.Text(fact, "kind");
        var file = BugFixFacts.Text(fact, "file");
        if (file is null || kind is not ("test" or "ui"))
        {
            return null;
        }

        if (Command(fact) is not { } command || (kind == "test" && command.Count == 0))
        {
            return null;
        }

        return new FrozenCheck(kind, file, command, BugFixFacts.Text(fact, "directory") ?? ".");
    }

    private static List<string>? Command(JsonNode? fact)
    {
        if (fact is not JsonObject { } obj || obj["command"] is not JsonArray items)
        {
            return [];
        }

        var command = new List<string>();
        foreach (var item in items)
        {
            if (item is not JsonValue value || !value.TryGetValue<string>(out var text))
            {
                return null;
            }

            command.Add(text);
        }

        return command;
    }
}
