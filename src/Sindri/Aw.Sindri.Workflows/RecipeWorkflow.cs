using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Temporalio.Common;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The recipe interpreter: the one workflow type that runs every recipe (engine spec rev 8 §5.1). Workflow code is
/// deterministic: no I/O, clock or random except through 'Workflow.*'; every decision is P3's pure
/// <see cref="TransitionDecision"/>. Engine P4a: the first interpreter for 'workflow.step': it loads the recipe
/// through the 'LoadRecipe' local activity, enters the initial state, and runs each active state's Step as a 'RunStep'
/// activity (desk runtime: async completion, the run token in the activity id) until a terminal state. It mirrors the
/// start and every transition into the ledger through the 'RecordEvent' local activity (spec rev 8.1 §5.6) and upserts
/// the 'Item', 'Recipe', 'State' and 'NeedsYou' search attributes 'workflow list' queries. Parks, waits, answers, gates
/// and effects are P4's; a park here waits for ever. (Rev 8.1 §5.3: P4 replaces the async-completion desk activity with
/// a short activity that records the open task and the validated 'complete' Update.)
/// </summary>
[Workflow("RecipeWorkflow")]
internal sealed class RecipeWorkflow
{
    private const string Desk = "human:desk";
    private const string Timers = "service:workflow.timers";
    private static readonly TimeSpan _noTimeout = TimeSpan.FromDays(30);
    private WorkflowInstance? _instance;
    private int _reports;

    /// <summary>Interprets the recipe from its initial state to a terminal one.</summary>
    /// <param name="run">The instance.</param>
    /// <returns>The terminal state.</returns>
    [WorkflowRun]
    public async Task<RecipeResult> RunAsync(RecipeRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var address = InstanceAddress.From(run.Address);
        var recipeId = "workflow." + address.Value.Split('/')[1];
        var loaded = await Workflow.ExecuteLocalActivityAsync(
            (RunStepActivities a) => a.LoadRecipeAsync(recipeId),
            new LocalActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(30) }).ConfigureAwait(true);
        var recipe = loaded.Recipe;
        var context = new DecisionContext(ApprovalCheck.Holds, new BlockCatalog(loaded.Blocks, []), new HashSet<BlockId>(), NoChildRecipes.Instance);
        var instance = Started(TransitionDecision.Start(StartOf(address, run, recipe), recipe, context));
        _instance = instance;
        var item = address.Value.Split('/')[0];
        await Mirror(new WorkflowEventRecord(address.Value, "workflow.started", item, string.Empty, instance.State, "start", Outcome: null, Run: null), recipeId, NeedsYou(instance)).ConfigureAwait(true);
        while (!instance.Ended)
        {
            if (instance.Suspension is not null || instance.Frame.Run is not { } open)
            {
                await Workflow.WaitConditionAsync(static () => false).ConfigureAwait(true); // P4: parks, waits and answers
                continue;
            }

            var trigger = await RunStep(address, instance.Frame, open, recipe.States[instance.Frame.State]).ConfigureAwait(true);
            var before = instance;
            instance = After(instance, TransitionDecision.Decide(new DecisionInput(instance, recipe, trigger, GateAnswers.None, context)));
            _instance = instance;
            if (!ReferenceEquals(before, instance))
            {
                var (kind, outcome) = trigger switch
                {
                    StepCompleted c => (TriggerNames.StepCompleted, c.Outcome.Value),
                    StepFailed => (TriggerNames.StepFailed, null),
                    _ => (TriggerNames.Deadline, null),
                };
                var record = new WorkflowEventRecord(address.Value, "workflow.transition", item, before.State, instance.State, kind, outcome, open.Token.Value);
                await Mirror(record, recipeId, NeedsYou(instance)).ConfigureAwait(true);
            }
        }

        return new RecipeResult(instance.State, instance.Outcome?.Value);
    }

    /// <summary>
    /// Mirrors one event into the ledger ('RecordEvent', a local activity keyed by its own activity id; rev 8.1 §5.6),
    /// then upserts the search attributes the instance is found by.
    /// </summary>
    private static async Task Mirror(WorkflowEventRecord record, string recipeId, bool needsYou)
    {
        _ = await Workflow.ExecuteLocalActivityAsync((RecordEventActivities a) => a.RecordAsync(record), RecordEventActivities.LocalOptions).ConfigureAwait(true);
        Workflow.UpsertTypedSearchAttributes(
            SindriTemporal.ItemKey.ValueSet(record.Item),
            SindriTemporal.RecipeKey.ValueSet(recipeId),
            SindriTemporal.StateKey.ValueSet(record.To),
            SindriTemporal.NeedsYouKey.ValueSet(needsYou));
    }

    /// <summary>A desk run waits on a human: the instance is in an active state with an open run.</summary>
    private static bool NeedsYou(WorkflowInstance instance) => !instance.Ended && instance.Frame.Run is not null;

    /// <summary>The instance as status shows it (the 'GetInstance' Query, spec §5.1).</summary>
    /// <returns>The state, the open run token and the outcome.</returns>
    [WorkflowQuery("GetInstance")]
    public InstanceSnapshot GetInstance() => _instance is { } i
        ? new InstanceSnapshot(i.State, i.Frame.Run?.Token.Value, i.Outcome?.Value)
        : new InstanceSnapshot("starting", Run: null, Outcome: null);

    /// <summary>
    /// Runs one Step: the RunStep activity with the run token in its id, at most once (a timeout is the state's
    /// deadline, not a retry), closing by the state's timeout. Its report is the 'step.completed' trigger.
    /// </summary>
    private async Task<Trigger> RunStep(InstanceAddress address, Frame frame, OpenRun open, RecipeState state)
    {
        _reports++;
        var step = new StepRun(address.Value, frame.State, open.Token.Value);
        var options = new ActivityOptions
        {
            ActivityId = string.Create(CultureInfo.InvariantCulture, $"run:{open.Token.Value}:{_reports}"),
            ScheduleToCloseTimeout = state.Timeout?.After.Span ?? _noTimeout,
            RetryPolicy = new RetryPolicy { MaximumAttempts = 1 },
        };
        try
        {
            var report = await Workflow.ExecuteActivityAsync((RunStepActivities a) => a.RunStepAsync(step), options).ConfigureAwait(true);
            return Completed(open.Token, report);
        }
        catch (ActivityFailureException e) when (e.InnerException is TimeoutFailureException)
        {
            return new DeadlineFired(Timers, frame.Entry, DeadlineKind.State);
        }
    }

    private static Trigger Completed(RunToken run, StepReport report)
    {
        var sha = ReportSha256(report);
        if (!OutcomeName.TryFrom(report.Outcome, out var outcome) || report.Outputs.Keys.Any(static k => !FactKey.TryFrom(k, out _)))
        {
            return new StepFailed(Desk, run, $"a malformed report: outcome '{report.Outcome}'", Tokens: 0, sha);
        }

        var outputs = report.Outputs.ToDictionary(static o => FactKey.From(o.Key), static o => o.Value);
        return new StepCompleted(Desk, run, outcome, outputs, Tokens: 0, StepRuntime.Desk, sha);
    }

    /// <summary>The SHA-256 of the report's canonical text, so two different reports never share a trigger key.</summary>
    private static string ReportSha256(StepReport report)
    {
        var text = new StringBuilder(report.Outcome);
        foreach (var (fact, value) in report.Outputs.OrderBy(static o => o.Key, StringComparer.Ordinal))
        {
            _ = text.Append('\n').Append(fact).Append('=').Append(value.Sha256);
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    private static StartRequest StartOf(InstanceAddress address, RecipeRun run, Recipe recipe)
    {
        var parts = address.Value.Split('/');
        var id = WorkflowInstanceId.For(new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(address.Value)).AsSpan(0, 16)));
        var hash = recipe.Hash ?? throw new InvalidOperationException($"LoadRecipe returned {recipe.Id.Value} without its hash");
        return new StartRequest(
            TriggerKey.For(id, "start"),
            id,
            address,
            new PinnedRecipe(recipe.Id, recipe.Version, hash, new Dictionary<RecipeId, RecipeHash>()),
            ItemKey.From(parts[0]),
            int.Parse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture),
            Parent: null,
            run.Inputs.ToDictionary(static i => FactKey.From(i.Key), static i => i.Value),
            Desk,
            TakenOver: false);
    }

    private static WorkflowInstance Started(Decision decision) => decision switch
    {
        Applied applied => applied.Changes[0].After,
        Rejected rejected => throw new ApplicationFailureException($"the start was rejected: {rejected.Error.Code} {rejected.Error.Message}", nonRetryable: true),
        _ => throw new ApplicationFailureException($"the start was {decision.GetType().Name}", nonRetryable: true),
    };

    /// <summary>The instance after a decision: an applied or refused trigger's own change; a rejected or absorbed one leaves it as it was.</summary>
    private static WorkflowInstance After(WorkflowInstance instance, Decision decision) => decision switch
    {
        Applied applied => Own(instance, applied.Changes),
        Refused refused => Own(instance, refused.Changes),
        _ => instance,
    };

    private static WorkflowInstance Own(WorkflowInstance instance, IReadOnlyList<InstanceChange> changes) =>
        changes.FirstOrDefault(c => c.After.Id == instance.Id)?.After ?? instance;
}
