using System.Text.Json.Nodes;
using Aw.Sindri.Core.BugFix;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.BugFix;

/// <summary>
/// 'step.bugfix.start-attempt' on the block runtime (DT spec 6.1): the route's 'fact:decision' (choice 'single' or 'split') opens bugfix-state's next attempt in that mode and is reported with
/// 'fact:attempt' ({"n":1,"mode":"single"}). The cap of 3 is bugfix-state's: its refusal of that kind is the outcome 'capReached' and writes nothing; any other refusal fails the Step with its reason.
/// </summary>
/// <param name="state">bugfix-state.</param>
/// <param name="area">The instance's run area, where its bug fix state lives.</param>
internal sealed class StartAttemptStep(IBugFixState state, IRunArea area) : IBlockStep, IBlockStepId
{
    private const string Name = "step.bugfix.start-attempt";

    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From(Name);

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var choice = BugFixFacts.Text(StepFacts.Read(context, "fact:decision"), "choice");
        if (choice is not ("single" or "split"))
        {
            return BlockStepResult.Failed($"{Name} needs fact:decision to choose 'single' or 'split', not '{choice}'");
        }

        var (directory, failure) = BugFixFacts.StateDirectory(Name, context, area);
        if (failure is not null)
        {
            return failure;
        }

        var mode = choice == "split" ? AttemptMode.Split : AttemptMode.Whole;
        var started = await state.StartAttempt(directory, mode, cancellationToken).ConfigureAwait(false);
        return started switch
        {
            { Status: BugFixAnswerStatus.Ok, Value: { } attempt } => StepFacts.Done(choice, ("fact:attempt", new JsonObject { ["n"] = attempt.Attempt, ["mode"] = choice })),
            { Refusal.Kind: RefusalKind.AttemptCap } => StepFacts.Done("capReached"),
            _ => BugFixFacts.Failure(Name, started),
        };
    }
}
