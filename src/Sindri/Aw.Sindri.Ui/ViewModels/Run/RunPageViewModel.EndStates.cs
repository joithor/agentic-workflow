using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>The run page's ending (<see cref="RunPageViewModel"/>, continued): the failed, didn't-work-out and cancelled states, "What went wrong" read from the instance events, and the next options (F3 spec 3 §8.5).</summary>
internal sealed partial class RunPageViewModel
{
    private string? _failureInstance;   // the instance whose events say why: the failed stage's child, else the line
    private string? _failureToken;      // the run token of the failed step, when its instance served one
    private long _failureAt;            // a Show counter: an events answer for an older Show is dropped
    private bool _failureHeard;

    /// <summary>True while a failed step is shown with its reason (or the line that none was recorded).</summary>
    public bool HasFailure { get; private set => Set(ref field, value); }

    /// <summary>The failed step's name, or the stage's when no step failed.</summary>
    public string FailureStep { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Why it failed, in words.</summary>
    public string FailureText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The block's heading.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string FailureTitle => Labels.RunWhatWentWrong;
#pragma warning restore CA1822, MA0041

    /// <summary>What the person can do next: only what the server serves, and the run's context.</summary>
    public IReadOnlyList<RunNextOption> NextOptions { get; private set => Set(ref field, value); } = [];

    /// <summary>True when the run has ended (a reached end card exists).</summary>
    public bool HasEnded { get; private set => Set(ref field, value); }

    // Called by Show once the cards are known: the header, Cancel, the failure block's frame and the next options.
    private void EndStates(InstanceDetail line, RunGraph graph, IReadOnlyDictionary<string, InstanceDetail> children)
    {
        var end = graph.Nodes.FirstOrDefault(n => n.Kind == RunNodeKind.End && IsReached(line, n));
        HasEnded = end is not null;
        var bad = RunEndStates.BadStage(graph);
        var failed = bad is { State: RunNodeState.Failed } || end?.State == RunNodeState.Failed || (end?.State == RunNodeState.DidntWorkOut && bad is not null);
        _failureAt++;
        _failureHeard = false;
        (_failureInstance, _failureToken) = failed ? FailureSource(line, bad, children) : (null, null);
        HasFailure = failed;
        FailureStep = bad is null ? string.Empty : bad.Steps.FirstOrDefault(s => s.State == RunNodeState.Failed)?.Name ?? bad.Name;
        FailureText = failed ? Labels.RunNoDetail : string.Empty;
        NextOptions = end is null ? [] : OptionsOf(line);
        if (end is not null)
        {
            Cancel = null;   // nothing left to cancel
            StartedText = Took(line, children);
        }
    }

    // The failed stage's child holds the step runs; with none read the line's own events are all there is.
    private static (string Instance, string? Token) FailureSource(InstanceDetail line, RunNode? bad, IReadOnlyDictionary<string, InstanceDetail> children)
    {
        if (bad?.ChildId is { } id && children.TryGetValue(id, out var child))
        {
            var run = child.StepRuns?.LastOrDefault(r => r.State is "failed" or "refused");
            return (id, run?.Token);
        }

        return (bad?.ChildId ?? line.Id, null);
    }

    private string Took(InstanceDetail line, IReadOnlyDictionary<string, InstanceDetail> children)
    {
        var began = children.Values.SelectMany(c => c.StepRuns ?? []).Select(r => (DateTimeOffset?)r.StartedAt).Min();
        return began is { } from
            ? Labels.RunTook(Labels.Duration((long)Math.Max(0, (line.Since - from).TotalSeconds)))
            : Labels.RunEndedAgo(Labels.Duration((long)Math.Max(0, (clock.GetUtcNow() - line.Since).TotalSeconds)));
    }

    // Only what is served: the run's context, and no action this page does not know how to draw (takeover and a re-run wait on the engine, spec 3 §9).
    private static IReadOnlyList<RunNextOption> OptionsOf(InstanceDetail line) =>
        line.ItemId is { } item ? [new RunNextOption(Labels.RunSeeContext, new DeepLink(DeepLinkKind.Item, item))] : [];

    // Reads the events of the failed instance and puts the reason of its last failed step in the block.
    private async Task ReadWhatWentWrong(Uri url, long at, CancellationToken cancellationToken)
    {
        if (!HasFailure || _failureInstance is not { } instance || _failureHeard)
        {
            return;
        }

        var events = await api.InstanceEvents(url, instance, cancellationToken).ConfigureAwait(true);
        if (at != _failureAt || events.IsError)
        {
            return;
        }

        var failures = events.Value.Events.Where(e => string.Equals(e.Transition?.Trigger, "step.failed", StringComparison.Ordinal)).ToList();
        var own = failures.LastOrDefault(e => _failureToken is not null && string.Equals(e.Transition?.Run, _failureToken, StringComparison.Ordinal));
        if ((own ?? failures.LastOrDefault())?.Transition?.Reason is { Length: > 0 } reason)
        {
            FailureText = Labels.RunReason(reason);
        }

        _failureHeard = true;
    }
}

/// <summary>One thing to do after a run ended.</summary>
/// <param name="Text">The words.</param>
/// <param name="Link">Where it goes.</param>
internal sealed record RunNextOption(string Text, DeepLink Link);
