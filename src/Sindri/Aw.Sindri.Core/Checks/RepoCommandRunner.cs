using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Locks;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Checks;

/// <summary>
/// Runs the repo's commands (<c language="text">workflow.build.commands</c>, line spec LN-WF9) for a build or ship Step: reads them from its source and runs a batch in the phase worktree
/// while holding one heavy slot, released on every path.
/// </summary>
/// <param name="source">The repo's commands.</param>
/// <param name="commands">Runs one command.</param>
/// <param name="slots">The box-wide heavy-job lock.</param>
/// <param name="clock">Times the budget.</param>
public sealed class RepoCommandRunner(IBuildCommandSource source, ICheckCommands commands, IHeavySlots slots, TimeProvider clock)
{
    private static readonly TimeSpan _reportMargin = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan _slotWait = TimeSpan.FromMinutes(30);

    /// <summary>The budget a Step gives its commands: its MaxDuration less a margin to write its report.</summary>
    /// <param name="maxDuration">The Step's MaxDuration.</param>
    /// <returns>The time the slot wait and every command share.</returns>
    public static TimeSpan BudgetFor(TimeSpan maxDuration) => maxDuration > _reportMargin ? maxDuration - _reportMargin : TimeSpan.Zero;

    /// <summary>The commands the approved profile lists for a stage, in the profile's order.</summary>
    /// <param name="stage">'red', 'checks' or 'green' (<see cref="BuildCommand.Where"/>).</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The commands; none when no profile is approved or it lists none for the stage.</returns>
    public ValueTask<Result<IReadOnlyList<BuildCommand>>> For(string stage, CancellationToken cancellationToken) => source.For(stage, cancellationToken);

    /// <summary>Runs every planned command in order, in <paramref name="workingDirectory"/>, under one heavy slot. A failing command does not stop the rest.</summary>
    /// <param name="holder">Who holds the slot, for the lock's owner file ('sindri step.build.checks').</param>
    /// <param name="plan">The commands and the arguments added to each.</param>
    /// <param name="workingDirectory">The phase worktree.</param>
    /// <param name="budget">The time the slot wait and every command share (<see cref="BudgetFor"/>); a command gets what is left of it.</param>
    /// <param name="cancellationToken">Cancels the batch: the running command is killed and the call throws.</param>
    /// <returns>How each ended; an error when the slot stayed busy, the budget ran out or a command could not be started.</returns>
    public async ValueTask<Result<IReadOnlyList<CheckResult>>> Run(
        string holder, IReadOnlyList<PlannedCommand> plan, string workingDirectory, TimeSpan budget, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.Count == 0)
        {
            return Results.Success<IReadOnlyList<CheckResult>>([]);
        }

        var deadline = clock.GetUtcNow() + budget;

        // Until the engine grants a recipe-level slot wait (LN-WF3, `waitFor: { resource: heavy }`), the Step takes its own slot here. When LN-WF3 lands this must go: the instance
        // would hold two slots, and the box's cap counts both.
        var wait = budget < _slotWait ? budget : _slotWait;
        var taken = await slots.Take(holder, wait, waiting: null, cancellationToken).ConfigureAwait(false);
        if (taken.IsError)
        {
            return taken.Error;
        }

        if (taken.Value.Lease is not { } lease)
        {
            return WorkflowErrors.EffectFailed.With($"every heavy slot stayed busy for {Minutes(wait)} min: {taken.Value.Busy}");
        }

        await using (lease.ConfigureAwait(false))
        {
            return await RunAll(plan, workingDirectory, new Allowance(deadline, budget), cancellationToken).ConfigureAwait(false);
        }
    }

    private static string Minutes(TimeSpan time) => time.TotalMinutes.ToString("0", CultureInfo.InvariantCulture);

    private static Error OutOfBudget(Allowance allowance, string what) =>
        WorkflowErrors.EffectFailed.With($"the Step's budget of {Minutes(allowance.Budget)} min ran out {what}");

    private async ValueTask<Result<IReadOnlyList<CheckResult>>> RunAll(IReadOnlyList<PlannedCommand> plan, string workingDirectory, Allowance allowance, CancellationToken cancellationToken)
    {
        var results = new List<CheckResult>(plan.Count);
        foreach (var planned in plan)
        {
            var left = allowance.Deadline - clock.GetUtcNow();
            if (left <= TimeSpan.Zero)
            {
                return OutOfBudget(allowance, $"before '{planned.Command.Name}' could start ({results.Count} of {plan.Count} commands ran)");
            }

            var own = planned.Command.Timeout is { } text && RecipeDuration.TryFrom(text, out var parsed) && parsed.Span < left ? parsed.Span : (TimeSpan?)null;
            var invocation = new CheckInvocation(planned.Command.Name, [.. planned.Command.Run, .. planned.ExtraArguments], own ?? left);
            var run = await commands.Run(invocation, workingDirectory, cancellationToken).ConfigureAwait(false);
            if (run.IsError)
            {
                return run.Error;
            }

            if (own is null && run.Value.TimedOut)
            {
                return OutOfBudget(allowance, $"while '{planned.Command.Name}' was running ({results.Count} of {plan.Count} commands ran before it){TailOf(run.Value)}");
            }

            results.Add(new CheckResult(planned, run.Value));
        }

        return results;
    }

    private static string TailOf(CheckRun run) => string.IsNullOrWhiteSpace(run.Tail) ? string.Empty : $"; it had printed: {run.Tail}";

    private sealed record Allowance(DateTimeOffset Deadline, TimeSpan Budget);
}
