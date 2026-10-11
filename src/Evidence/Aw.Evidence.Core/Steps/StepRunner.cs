using Aw.Evidence.Core.Browser;
using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Ids;
using Aw.Evidence.Core.Judging;
using Aw.Evidence.Core.Scripts;
using Aw.Evidence.Core.Usage;
using Aw.Kernel;

namespace Aw.Evidence.Core.Steps;

/// <summary>
/// Runs one step and checks its expected state. The action failing means the selector is broken: a click or fill may
/// ask the judge for a visible replacement (at most <see cref="RepairRules.MaxAttempts"/> times), run the same action
/// on it and re-check the expectation (only a repaired action that fails gets another attempt; one that runs settles the
/// step: passed, or failed when the expectation misses); where writes are refused, a candidate that reads like a write is never acted on
/// (it counts as no candidate). The action passing but the expectation failing is a behaviour failure:
/// <see cref="StepStatus.Failed"/>, never repaired or masked by a model.
/// </summary>
/// <param name="judge">The judge that proposes repairs.</param>
/// <param name="clock">Times the repair calls.</param>
public sealed class StepRunner(IJudge judge, TimeProvider clock)
{
    /// <summary>Runs <paramref name="step"/>.</summary>
    /// <param name="session">The page.</param>
    /// <param name="step">The step.</param>
    /// <param name="options">Timeouts and whether repair is allowed.</param>
    /// <param name="log">Where repair calls are recorded.</param>
    /// <param name="cancellationToken">Cancels the step.</param>
    /// <returns>How the step ended.</returns>
    public async ValueTask<StepStatus> RunAsync(
        IBrowserSession session, ScriptStep step, StepRunOptions options, InvocationLog log, CancellationToken cancellationToken)
    {
        var acted = await PerformAsync(session, step, TargetOf(step), options.ActionTimeout, cancellationToken).ConfigureAwait(false);
        if (acted.IsError)
        {
            // No judge wired: no repair is tried, and no model call is recorded.
            return options.AllowRepair && RepairRules.IsRepairable(step.Action) && judge.IsAvailable
                ? await RepairAsync(session, step, options, log, cancellationToken).ConfigureAwait(false)
                : StepStatus.Broken;
        }

        return await ExpectationHolds(session, step.ExpectedState, options.ExpectationTimeout, cancellationToken).ConfigureAwait(false)
            ? StepStatus.Passed
            : StepStatus.Failed;
    }

    /// <summary>Checks an expected state against the page.</summary>
    /// <param name="session">The page.</param>
    /// <param name="expected">The state.</param>
    /// <param name="timeout">How long it may take to hold.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>Whether it holds.</returns>
    public static async ValueTask<bool> ExpectationHolds(IBrowserSession session, ExpectedState expected, TimeSpan timeout, CancellationToken cancellationToken)
    {
        switch (expected)
        {
            case InputValue input:
                var actual = await session.InputValueAsync(Locator.ByTestId(input.TestId), timeout, cancellationToken).ConfigureAwait(false);
                return actual.IsSuccess && string.Equals(actual.Value, input.Value, StringComparison.Ordinal);
            case TextVisible visible:
                return (await session.WaitVisibleAsync(Locator.ByText(visible.Text), timeout, cancellationToken).ConfigureAwait(false)).IsSuccess;
            case TextAbsent absent:
                return (await session.WaitHiddenAsync(Locator.ByText(absent.Text), timeout, cancellationToken).ConfigureAwait(false)).IsSuccess;
            case TestIdVisible element:
                return (await session.WaitVisibleAsync(Locator.ByTestId(element.TestId), timeout, cancellationToken).ConfigureAwait(false)).IsSuccess;
            case UrlPath path:
                return (await session.WaitForPathAsync(path.Path, timeout, cancellationToken).ConfigureAwait(false)).IsSuccess;
            default:
                return false;
        }
    }

    private static Locator? TargetOf(ScriptStep step) => step.Action switch
    {
        StepAction.Click or StepAction.Fill => TestId.TryFrom(step.Target, out var id) ? Locator.ByTestId(id) : null,
        StepAction.ExpectVisible => Locator.ByText(step.Target),
        StepAction.Goto => null,
        _ => null,
    };

    private static async ValueTask<Result<Unit>> PerformAsync(
        IBrowserSession session, ScriptStep step, Locator? target, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (step.Action == StepAction.Goto)
        {
            // Defence in depth behind StepGuard: a target never rewrites the host ('//evil', '@evil').
            return StepGuard.IsSameOriginPath(step.Target)
                ? await session.GotoAsync(step.Target, timeout, cancellationToken).ConfigureAwait(false)
                : EvidenceErrors.StepsRefused.With($"goto target must be a path on the host: {step.Target}");
        }

        if (target is null)
        {
            return EvidenceErrors.BrowserFailed.With($"{step.ActionName} has a blank target");
        }

        return step.Action switch
        {
            StepAction.Click => await session.ClickAsync(target, timeout, cancellationToken).ConfigureAwait(false),
            StepAction.Fill => await session.FillAsync(target, step.Value ?? string.Empty, timeout, cancellationToken).ConfigureAwait(false),
            StepAction.Goto or StepAction.ExpectVisible => await session.WaitVisibleAsync(target, timeout, cancellationToken).ConfigureAwait(false),
            _ => EvidenceErrors.BrowserFailed.With($"unknown action {step.ActionName}"),
        };
    }

    private async ValueTask<StepStatus> RepairAsync(
        IBrowserSession session, ScriptStep step, StepRunOptions options, InvocationLog log, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < RepairRules.MaxAttempts; attempt++)
        {
            var chosen = await ProposeAsync(session, step, log, cancellationToken).ConfigureAwait(false);
            if (chosen is null || !RepairRules.Fits(step.Action, chosen, options.RefuseWrites))
            {
                continue;
            }

            var target = new CandidateLocator(chosen.Index, chosen.TestId);
            var acted = await PerformAsync(session, step, target, options.ActionTimeout, cancellationToken).ConfigureAwait(false);
            if (acted.IsSuccess)
            {
                // The repaired action ran, so the page changed: the expectation decides, and nothing acts again.
                return await ExpectationHolds(session, step.ExpectedState, options.ExpectationTimeout, cancellationToken).ConfigureAwait(false)
                    ? StepStatus.Passed
                    : StepStatus.Failed;
            }
        }

        return StepStatus.Broken;
    }

    private async ValueTask<Candidate?> ProposeAsync(IBrowserSession session, ScriptStep step, InvocationLog log, CancellationToken cancellationToken)
    {
        var visible = await session.VisibleCandidatesAsync(cancellationToken).ConfigureAwait(false);
        if (visible.IsError)
        {
            return null;
        }

        var request = new SelectorRepairRequest(step.Target, step.Describe(), visible.Value);
        var proposal = await log.TimeAsync(
            InvocationPhase.SelectorRepair,
            clock,
            () => judge.RepairSelectorAsync(request, cancellationToken),
            static r => r.Usage).ConfigureAwait(false);
        return RepairRules.Resolve(proposal, visible.Value);
    }
}
