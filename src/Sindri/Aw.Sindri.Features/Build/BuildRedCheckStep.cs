using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Build;

/// <summary>
/// 'step.build.red-check' on the block runtime (line spec §7.2, BLD-RED-001): every red test of every item must FAIL on the red commit, before any code. For each item the profile's
/// <c language="text">on: red</c> commands run in the phase worktree with the item's test ids appended; an item whose command exits 0 is 'vacuous' (a finding names it) and the build goes back to 'red'.
/// A command that runs out of time, a worktree off the red commit, no red command in the profile or no test to run fails the Step instead, so the instance parks. The commands run under one
/// heavy slot. An item with only an 'appOnly' signal has no test and is not run.
/// </summary>
/// <param name="diff">Reads the worktree's head.</param>
/// <param name="commands">Reads the profile's red commands and runs them under a heavy slot.</param>
internal sealed class BuildRedCheckStep(IRunDiff diff, RepoCommandRunner commands) : IBlockStep, IBlockStepId
{
    /// <summary>The longest one command may run when the profile sets no timeout for it.</summary>
    internal static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(40);

    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.build.red-check");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (ActivePacket.From(StepFacts.Read(context, "fact:activePacket")) is not { Repo: { } worktree })
        {
            return BlockStepResult.Failed("step.build.red-check needs fact:activePacket with the worktree as its repo (step.build.prepare sets it)");
        }

        var red = RedTests.From(StepFacts.Read(context, "fact:redTests"));
        if (red.Items.Count == 0)
        {
            return BlockStepResult.Failed("step.build.red-check found no test to run in fact:redTests ({ items: { <item>: { tests: [ids] } } })");
        }

        var listed = await commands.For(BuildCommand.Red, cancellationToken).ConfigureAwait(false);
        if (listed.IsError || listed.Value.Count == 0)
        {
            return BlockStepResult.Failed(listed.IsError ? listed.Error.Message : "step.build.red-check needs a command with 'on: red' in the profile's workflow.build.commands (fix: add the repo's test command; the item's test ids are appended to it)");
        }

        return await CheckHead(red, worktree, cancellationToken).ConfigureAwait(false) is { } off
            ? off
            : await Check(red, listed.Value, worktree, cancellationToken).ConfigureAwait(false);
    }

    // The tests must run on the red commit: a worktree that already moved on proves nothing.
    private async ValueTask<BlockStepResult?> CheckHead(RedTests red, string worktree, CancellationToken cancellationToken)
    {
        if (red.Commit is null)
        {
            return null;
        }

        var head = await diff.Head(worktree, cancellationToken).ConfigureAwait(false);
        return head.IsError
            ? BlockStepResult.Failed($"step.build.red-check can't read {worktree}: {head.Error.Message}")
            : string.Equals(head.Value, red.Commit, StringComparison.Ordinal)
                ? null
                : BlockStepResult.Failed($"step.build.red-check: {worktree} is at {head.Value}, not the red commit {red.Commit} (BLD-RED-001 needs the tests run before any code)");
    }

    private async ValueTask<BlockStepResult> Check(RedTests red, IReadOnlyList<BuildCommand> redCommands, string worktree, CancellationToken cancellationToken)
    {
        var plan = red.Items.SelectMany(item => redCommands.Select(command => new PlannedCommand(command, item.Tests))).ToList();
        var ran = await commands.Run("sindri step.build.red-check", plan, worktree, CommandTimeout, cancellationToken).ConfigureAwait(false);
        if (ran.IsError)
        {
            return BlockStepResult.Failed($"step.build.red-check: {ran.Error.Message}");
        }

        if (ran.Value.FirstOrDefault(static r => r.Run.TimedOut) is { } slow)
        {
            return BlockStepResult.Failed($"step.build.red-check: {slow.Summary}, so the red tests can't be judged (fix: raise its timeout in workflow.build.commands)");
        }

        var vacuous = red.Items
            .Select((item, i) => (Item: item, Results: ran.Value.Skip(i * redCommands.Count).Take(redCommands.Count).ToList()))
            .Where(static x => x.Results.Exists(static r => r.Run.Passed))
            .Select(static x => new RuleFinding(
                "BLD-RED-001",
                $"the red tests of item {x.Item.Id} ({string.Join(", ", x.Item.Tests)}) passed on the red commit; a test that can't fail proves nothing",
                Detail: x.Results.First(static r => r.Run.Passed).Run.Tail))
            .ToList();
        return vacuous.Count == 0 ? StepFacts.Done("red") : StepFacts.Done("vacuous", ("fact:findings", RuleFinding.ToFact(vacuous)));
    }
}
