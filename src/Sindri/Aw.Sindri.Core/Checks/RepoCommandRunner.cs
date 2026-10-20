using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Locks;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Checks;

/// <summary>
/// Runs the repo's commands (<c language="text">workflow.build.commands</c>, line spec LN-WF9) for a build or ship Step: reads them from the approved profile and runs a batch in the phase worktree
/// while holding one heavy slot, released on every path.
/// </summary>
/// <param name="profile">The approved profile.</param>
/// <param name="commands">Runs one command.</param>
/// <param name="slots">The box-wide heavy-job lock.</param>
public sealed class RepoCommandRunner(IApprovedProfile profile, ICheckCommands commands, IHeavySlots slots)
{
    private static readonly TimeSpan _slotWait = TimeSpan.FromMinutes(30);

    /// <summary>The commands the approved profile lists for a stage, in the profile's order.</summary>
    /// <param name="stage">'red', 'checks' or 'green' (<see cref="BuildCommand.Where"/>).</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The commands; none when no profile is approved or it lists none for the stage.</returns>
    public async ValueTask<Result<IReadOnlyList<BuildCommand>>> For(string stage, CancellationToken cancellationToken)
    {
        var latest = await profile.Latest(cancellationToken).ConfigureAwait(false);
        if (latest.IsError)
        {
            return latest.Error;
        }

        IReadOnlyList<BuildCommand> found = [.. latest.Value?.Profile.Workflow?.Build?.Commands?.Where(c => string.Equals(c.Where, stage, StringComparison.Ordinal)) ?? []];
        return Results.Success(found);
    }

    /// <summary>Runs every planned command in order, in <paramref name="workingDirectory"/>, under one heavy slot. A failing command does not stop the rest.</summary>
    /// <param name="holder">Who holds the slot, for the lock's owner file ('sindri step.build.checks').</param>
    /// <param name="plan">The commands and the arguments added to each.</param>
    /// <param name="workingDirectory">The phase worktree.</param>
    /// <param name="fallbackTimeout">The timeout of a command whose profile entry sets none.</param>
    /// <param name="cancellationToken">Cancels the batch: the running command is killed and the call throws.</param>
    /// <returns>How each ended; an error when the slot stayed busy or a command could not be started.</returns>
    public async ValueTask<Result<IReadOnlyList<CommandResult>>> Run(
        string holder, IReadOnlyList<PlannedCommand> plan, string workingDirectory, TimeSpan fallbackTimeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.Count == 0)
        {
            return Results.Success<IReadOnlyList<CommandResult>>([]);
        }

        // Until the engine grants a recipe-level slot wait (LN-WF3, `waitFor: { resource: heavy }`), the Step takes its own slot here. When LN-WF3 lands this must go: the instance
        // would hold two slots, and the box's cap counts both.
        var taken = await slots.Take(holder, _slotWait, waiting: null, cancellationToken).ConfigureAwait(false);
        if (taken.IsError)
        {
            return taken.Error;
        }

        if (taken.Value.Lease is not { } lease)
        {
            return WorkflowErrors.EffectFailed.With($"every heavy slot stayed busy for {_slotWait.TotalMinutes.ToString("0", CultureInfo.InvariantCulture)} min: {taken.Value.Busy}");
        }

        await using (lease.ConfigureAwait(false))
        {
            return await RunAll(plan, workingDirectory, fallbackTimeout, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<Result<IReadOnlyList<CommandResult>>> RunAll(IReadOnlyList<PlannedCommand> plan, string workingDirectory, TimeSpan fallbackTimeout, CancellationToken cancellationToken)
    {
        var results = new List<CommandResult>(plan.Count);
        foreach (var planned in plan)
        {
            var timeout = planned.Command.Timeout is { } text && RecipeDuration.TryFrom(text, out var parsed) ? parsed.Span : fallbackTimeout;
            var invocation = new CheckInvocation(planned.Command.Name, [.. planned.Command.Run, .. planned.ExtraArguments], timeout);
            var run = await commands.Run(invocation, workingDirectory, cancellationToken).ConfigureAwait(false);
            if (run.IsError)
            {
                return run.Error;
            }

            results.Add(new CommandResult(planned, run.Value));
        }

        return results;
    }
}
