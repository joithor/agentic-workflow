using System.Globalization;
using Aw.Sindri.Infrastructure.Workflows;
using Temporalio.Workflows;
using Wf = Temporalio.Workflows.Workflow;

namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>
/// The throwaway test-only interpreter for 'workflow.step' (engine P0 pass line 2). Unit (b) builds the real throwaway
/// RecipeWorkflow in parallel; this one exists so unit (c) isn't blocked. Each active state runs the desk Step as an
/// async-completion activity whose id is the run token '{entry}.1', races it with Workflow.DelayAsync(timeout), decides
/// with <see cref="SpikeDecision"/> and mirrors every start and transition into the ledger through the 'RecordEvent'
/// local activity. A park ends the run (P4 keeps it open for answers); a timeout parks ('then: retry' isn't modelled).
/// </summary>
[Workflow("SpikeStep")]
internal sealed class SpikeStepWorkflow
{
    private readonly Dictionary<string, int> _rounds = new(StringComparer.Ordinal);
    private string? _findings;
    private int _sequence;

    /// <summary>The instance as the query shows it.</summary>
    [WorkflowQuery]
    public SpikeView View { get; private set; } = new(string.Empty, Park: null, Run: null, Entry: 0);

    /// <summary>Walks the recipe from its initial state to a terminal state or a park.</summary>
    /// <param name="run">The item and the recipe.</param>
    /// <returns>Where it ended.</returns>
    [WorkflowRun]
    public async Task<SpikeView> RunAsync(SpikeRun run)
    {
        var recipe = run.Recipe;
        if (recipe.State(recipe.Initial).Loop is { } loop)
        {
            _rounds[loop] = 1;
        }

        await Mirror(run, "workflow.started", string.Empty, recipe.Initial, "start", outcome: null, park: null);
        while (View.Park is null && !recipe.State(View.State).Terminal)
        {
            var state = recipe.State(View.State);
            var entry = View.Entry + 1;
            var token = string.Create(CultureInfo.InvariantCulture, $"{entry}.1");
            View = View with { Entry = entry, Run = token };
            var report = await RunStep(state, token);
            if (report is null)
            {
                await Mirror(run, "workflow.transition", state.Name, state.Name, "timeout", outcome: null, park: "timeout");
                continue;
            }

            var verdict = SpikeDecision.Decide(recipe, state.Name, report, _rounds, _findings);
            _findings = verdict.Findings;
            if (verdict is { Park: null, Loop: { } counted })
            {
                _rounds[counted] = verdict.Round;
            }

            await Mirror(run, "workflow.transition", state.Name, verdict.To, "step.completed", report.Outcome, verdict.Park);
        }

        return View;
    }

    private static async Task<SpikeReport?> RunStep(SpikeState state, string token)
    {
        using var scope = CancellationTokenSource.CreateLinkedTokenSource(Wf.CancellationToken);
        var step = Wf.ExecuteActivityAsync(
            (SpikeDeskActivities a) => a.RunStep(new SpikeStepRun(Wf.Info.WorkflowId, state.Name, token)),
            new ActivityOptions { ActivityId = token, ScheduleToCloseTimeout = TimeSpan.FromDays(30), CancellationToken = scope.Token });
        var timer = Wf.DelayAsync(state.Timeout, scope.Token);
        var first = await Wf.WhenAnyAsync(step, timer);
#pragma warning disable MA0042 // why: CancelAsync runs callbacks off the workflow's deterministic scheduler; Cancel stays on it.
        scope.Cancel();
#pragma warning restore MA0042
        return first == step ? await step : null;
    }

    private async Task Mirror(SpikeRun run, string type, string from, string to, string trigger, string? outcome, string? park)
    {
        _sequence++;
        var entered = park is null ? to : "parked";
        var record = new WorkflowEventRecord(Wf.Info.WorkflowId, Wf.Info.RunId, _sequence, type, run.Item, from, entered, trigger, outcome, View.Run);
        _ = await Wf.ExecuteLocalActivityAsync((RecordEventActivities a) => a.RecordAsync(record), RecordEventActivities.LocalOptions);
        View = View with { State = entered, Park = park };
    }
}
