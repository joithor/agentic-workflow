using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The recipe interpreter: the one workflow type that runs every recipe (engine spec rev 8 §5.1). Its input is
/// <see cref="InstanceStart"/> and its workflow id is the instance address. Workflow code is deterministic: no I/O,
/// clock or random except through 'Workflow.*'; every decision is P3's pure <see cref="TransitionDecision"/>, and it
/// awaits with ConfigureAwait(true) (finding F3).
/// <para>
/// Engine P4 contract. It loads the pinned recipe through 'LoadRecipe', enters the initial state, and runs each active
/// state's desk Step as a short 'RunStep' activity (the run token in its id) followed by a wait for the 'complete'
/// Update, bounded by the state's timeout (rev 8.1 §5.3). It mirrors the start and every transition into the ledger
/// through 'RecordEvent' (§5.6) and upserts the search attributes. The Updates of §5.2 and the bridge's 'event'
/// signal (§5.4) have their final signatures; their bodies are the units' work: (a) the state loop over waits, parks
/// and answers; (b) 'answer', 'command', 'cancel' (and P5 'takeover', 'handback'); (c) 'complete' and its validator;
/// (d) 'event'.
/// </para>
/// </summary>
[Workflow("RecipeWorkflow")]
internal sealed class RecipeWorkflow
{
    private const string Desk = "human:desk";
    private const string Timers = "service:workflow.timers";
    private static readonly TimeSpan _noTimeout = TimeSpan.FromDays(30);
    private readonly Queue<WaitSatisfied> _delivered = new();
    private InstanceStart? _start;
    private InterpretedRecipe? _recipe;
    private WorkflowInstance? _instance;
    private DateTimeOffset _since;
    private DateTimeOffset? _timeoutAt;
    private Trigger? _report;
    private int _runs;

    /// <summary>Interprets the recipe from its initial state to a terminal one.</summary>
    /// <param name="start">The recipe, item, attempt, input facts, tenant and ring.</param>
    /// <returns>The terminal state.</returns>
    [WorkflowRun]
    public async Task<RecipeResult> RunAsync(InstanceStart start)
    {
        var (instance, context) = await Begin(start).ConfigureAwait(true);
        var recipe = context.Recipe;
        while (!instance.Ended)
        {
            if (instance.Suspension is not null || instance.Frame.Run is not { } open)
            {
                await Workflow.WaitConditionAsync(static () => false).ConfigureAwait(true); // unit (a): waits, parks and answers
                continue;
            }

            var trigger = await RunStep(instance.Frame, open, recipe.States[instance.Frame.State]).ConfigureAwait(true);
            var before = instance;
            instance = After(instance, TransitionDecision.Decide(new DecisionInput(instance, recipe, trigger, GateAnswers.None, context.Decisions)));
            if (!ReferenceEquals(before, instance))
            {
                Enter(instance);
                var (kind, outcome) = trigger switch
                {
                    StepCompleted c => (TriggerNames.StepCompleted, c.Outcome.Value),
                    StepFailed => (TriggerNames.StepFailed, null),
                    _ => (TriggerNames.Deadline, null),
                };
                var record = new WorkflowEventRecord(start.WorkflowId().Value, "workflow.transition", start.Item.Value, before.State, instance.State, kind, outcome, open.Token.Value);
                await Mirror(record, NeedsYou(instance)).ConfigureAwait(true);
            }
        }

        return new RecipeResult(instance.State, instance.Outcome?.Value);
    }

    /// <summary>
    /// Loads the pinned recipe ('LoadRecipe'), enters the initial state with the input's tenant and ring, mirrors
    /// 'workflow.started' into the ledger and sets the search attributes.
    /// </summary>
    private async Task<(WorkflowInstance Instance, Interpreting Context)> Begin(InstanceStart start)
    {
        ArgumentNullException.ThrowIfNull(start);
        _start = start;
        _since = Now;
        var loaded = await Workflow.ExecuteLocalActivityAsync(
            (RunStepActivities a) => a.LoadRecipeAsync(start.Recipe),
            new LocalActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(30) }).ConfigureAwait(true);
        var catalog = new BlockCatalog(loaded.Blocks, []);
        _recipe = new InterpretedRecipe(loaded.Version, loaded.Recipe, catalog, start.Inputs);
        var decisions = new DecisionContext(ApprovalCheck.Holds, catalog, new HashSet<BlockId>(), NoChildRecipes.Instance);
        var instance = Started(TransitionDecision.Start(StartOf(start, loaded), loaded.Recipe, decisions)) with { Tenant = start.Tenant, Ring = start.Ring };
        Enter(instance);
        Workflow.UpsertTypedSearchAttributes(
            SindriTemporal.TenantKey.ValueSet(start.Tenant),
            SindriTemporal.RingKey.ValueSet(start.Ring.ToString(CultureInfo.InvariantCulture)));
        await Mirror(new WorkflowEventRecord(start.WorkflowId().Value, "workflow.started", start.Item.Value, string.Empty, instance.State, "start", Outcome: null, Run: null), NeedsYou(instance)).ConfigureAwait(true);
        return (instance, new Interpreting(loaded.Recipe, decisions));
    }

    /// <summary>The instance as status shows it (the 'GetInstance' Query, spec §5.1, §6.3).</summary>
    /// <returns>The state, 'next', 'allowedCommands', the open question or desk task, the run token and the children.</returns>
    [WorkflowQuery("GetInstance")]
    public InstanceView GetInstance()
    {
        var start = _start ?? throw new InvalidOperationException("the instance has no input yet");
        return (_instance, _recipe) is ({ } instance, { } recipe)
            ? InstanceViews.Of(instance, recipe, _since, _timeoutAt)
            : new InstanceView(start.WorkflowId(), start.Recipe, "starting", Detail: null, _since, string.Empty, [], Question: null, Task: null, Run: null, Children: [], Outcome: null);
    }

    /// <summary>
    /// 'workflow complete' from the desk (spec rev 8.1 §5.3): the report for the open run, which the desk wait consumes.
    /// Update id: the nonce. Unit (c).
    /// </summary>
    /// <param name="report">The report: the run token, a declared outcome and the stored outputs.</param>
    /// <returns>The instance after the report, and what happened ('-> checks').</returns>
    [WorkflowUpdate("complete")]
    public Task<CommandResult> CompleteAsync(StepCompleted report) => throw Unbuilt("unit (c): the complete Update");

    /// <summary>Rejects, recording nothing: a run token that isn't the open run's (SND-WF-005), a malformed report (SND-WF-008). Unit (c).</summary>
    /// <param name="report">The report.</param>
    [WorkflowUpdateValidator(nameof(CompleteAsync))]
    public void ValidateComplete(StepCompleted report) => throw Unbuilt("unit (c): the complete validator");

    /// <summary>'workflow answer' in all its variants (spec §5.2, §5.8): ends the suspension it fits and runs its resume action. Unit (b).</summary>
    /// <param name="answer">The answer.</param>
    /// <returns>The instance after it, and the resume action it ran.</returns>
    [WorkflowUpdate("answer")]
    public Task<CommandResult> AnswerAsync(AnswerGiven answer) => throw Unbuilt("unit (b): the answer Update");

    /// <summary>Rejects an answer that doesn't fit the park reason (SND-WF-003, the fix naming the right answer). Unit (b).</summary>
    /// <param name="answer">The answer.</param>
    [WorkflowUpdateValidator(nameof(AnswerAsync))]
    public void ValidateAnswer(AnswerGiven answer) => throw Unbuilt("unit (b): the answer validator");

    /// <summary>'workflow command &lt;verb&gt;' (spec §3.5, §6.3): fires the recipe's 'command.&lt;verb&gt;' trigger. Unit (b).</summary>
    /// <param name="command">The command.</param>
    /// <returns>The instance after it, and what happened.</returns>
    [WorkflowUpdate("command")]
    public Task<CommandResult> CommandAsync(CommandFired command) => throw Unbuilt("unit (b): the command Update");

    /// <summary>Rejects a verb with no transition from the current state (SND-WF-003). Unit (b).</summary>
    /// <param name="command">The command.</param>
    [WorkflowUpdateValidator(nameof(CommandAsync))]
    public void ValidateCommand(CommandFired command) => throw Unbuilt("unit (b): the command validator");

    /// <summary>'workflow cancel' (spec §3.8): ends the instance as 'cancelled'; exempt from the approval check. Unit (b).</summary>
    /// <param name="cancel">The cancel.</param>
    /// <returns>The instance after it.</returns>
    [WorkflowUpdate("cancel")]
    public Task<CommandResult> CancelAsync(CancelRequested cancel) => throw Unbuilt("unit (b): the cancel Update");

    /// <summary>Rejects a cancel of an ended instance (SND-WF-003). Unit (b).</summary>
    /// <param name="cancel">The cancel.</param>
    [WorkflowUpdateValidator(nameof(CancelAsync))]
    public void ValidateCancel(CancelRequested cancel) => throw Unbuilt("unit (b): the cancel validator");

    /// <summary>'workflow takeover' (class pause, spec §5.5): suspends the instance and pauses its timer. P5.</summary>
    /// <param name="takeover">The takeover.</param>
    /// <returns>The instance after it.</returns>
    [WorkflowUpdate("takeover")]
    public Task<CommandResult> TakeoverAsync(TakeoverRequested takeover) => throw Unbuilt("P5: the takeover Update");

    /// <summary>Rejects a takeover where none is allowed (SND-WF-003). P5.</summary>
    /// <param name="takeover">The takeover.</param>
    [WorkflowUpdateValidator(nameof(TakeoverAsync))]
    public void ValidateTakeover(TakeoverRequested takeover) => throw Unbuilt("P5: the takeover validator");

    /// <summary>'workflow handback' (spec §5.5): ends the takeover and re-arms the timer with what remained. P5.</summary>
    /// <param name="handback">The handback.</param>
    /// <returns>The instance after it.</returns>
    [WorkflowUpdate("handback")]
    public Task<CommandResult> HandbackAsync(HandbackRequested handback) => throw Unbuilt("P5: the handback Update");

    /// <summary>Rejects a handback of an instance that isn't taken over (SND-WF-003). P5.</summary>
    /// <param name="handback">The handback.</param>
    [WorkflowUpdateValidator(nameof(HandbackAsync))]
    public void ValidateHandback(HandbackRequested handback) => throw Unbuilt("P5: the handback validator");

    /// <summary>
    /// A registered ledger event the bridge routed here (spec §5.4). Temporal buffers it until the instance reaches the
    /// wait; unit (a) consumes <see cref="_delivered"/> at a wait, unit (d) sends it and drops one older than 'eventHold'.
    /// </summary>
    /// <param name="delivered">The event as the wait's trigger.</param>
    /// <returns>Done.</returns>
    [WorkflowSignal("event")]
    public Task EventAsync(WaitSatisfied delivered)
    {
        ArgumentNullException.ThrowIfNull(delivered);
        _delivered.Enqueue(delivered);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Mirrors one event into the ledger ('RecordEvent', a local activity keyed by its own activity id; rev 8.1 §5.6),
    /// then upserts the search attributes the instance is found by.
    /// </summary>
    private async Task Mirror(WorkflowEventRecord record, bool needsYou)
    {
        _ = await Workflow.ExecuteLocalActivityAsync((RecordEventActivities a) => a.RecordAsync(record), RecordEventActivities.LocalOptions).ConfigureAwait(true);
        Workflow.UpsertTypedSearchAttributes(
            SindriTemporal.ItemKey.ValueSet(record.Item),
            SindriTemporal.RecipeKey.ValueSet(_start!.Recipe.Id.Value), // why: Mirror runs only after RunAsync set _start
            SindriTemporal.StateKey.ValueSet(record.To),
            SindriTemporal.NeedsYouKey.ValueSet(needsYou));
    }

    /// <summary>A desk run waits on a human: the instance is in an active state with an open run (parks and escalations: unit (a)).</summary>
    private static bool NeedsYou(WorkflowInstance instance) => !instance.Ended && instance.Frame.Run is not null;

    private void Enter(WorkflowInstance instance)
    {
        if (_instance is null || !string.Equals(_instance.State, instance.State, StringComparison.Ordinal) || _instance.Frame.Entry != instance.Frame.Entry)
        {
            _since = Now;
        }

        _instance = instance;
    }

    /// <summary>
    /// Runs one desk Step (rev 8.1 §5.3): the short 'RunStep' activity with the run token in its id records the open task,
    /// then the workflow waits for the 'complete' Update's report, bounded by the state's timeout (its expiry is the
    /// state's deadline). Unit (c) applies the 'workflow.deskTimeout' floor (S1-D11 is unit (e)'s).
    /// </summary>
    private async Task<Trigger> RunStep(Frame frame, OpenRun open, RecipeState state)
    {
        _runs++;
        var step = new StepRun(_start!.WorkflowId().Value, frame.State, open.Token.Value); // why: RunStep runs only after RunAsync set _start
        await Workflow.ExecuteActivityAsync(
            () => RunStepActivities.RunStepAsync(step),
            new ActivityOptions
            {
                ActivityId = string.Create(CultureInfo.InvariantCulture, $"run:{open.Token.Value}:{_runs}"),
                StartToCloseTimeout = TimeSpan.FromSeconds(30),
            }).ConfigureAwait(true);
        var timeout = state.Timeout?.After.Span ?? _noTimeout;
        _report = null;
        _timeoutAt = Now + timeout;
        var reported = await Workflow.WaitConditionAsync(() => _report is not null, timeout).ConfigureAwait(true);
        _timeoutAt = null;
        return reported && _report is { } report ? report : new DeadlineFired(Timers, frame.Entry, DeadlineKind.State);
    }

    /// <summary>The workflow's deterministic clock, as an offset in UTC.</summary>
    private static DateTimeOffset Now => new(Workflow.UtcNow, TimeSpan.Zero);

    /// <summary>The exception a stubbed handler throws: a rejected Update in a validator, recording nothing.</summary>
    private NotSupportedException Unbuilt(string what) =>
        new($"engine P4 {what} is not built yet ({_start?.WorkflowId().Value ?? "no instance"})");

    private static StartRequest StartOf(InstanceStart start, LoadedRecipe loaded)
    {
        var address = start.WorkflowId();
        var id = WorkflowInstanceId.For(new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(address.Value)).AsSpan(0, 16)));
        return new StartRequest(
            TriggerKey.For(id, "start"),
            id,
            address,
            new PinnedRecipe(loaded.Version.Id, loaded.Version.Version, loaded.Version.Hash, new Dictionary<RecipeId, RecipeHash>()),
            start.Item,
            start.Attempt,
            Parent: null,
            start.Inputs.ToDictionary(static i => i.Key, static i => i.Value.Value),
            Desk,
            TakenOver: false);
    }

    private static WorkflowInstance Started(Decision decision) => decision switch
    {
        Applied applied => applied.Changes[0].After,
        Rejected rejected => throw new ApplicationFailureException($"the start was rejected: {rejected.Error.Code} {rejected.Error.Message}", errorType: rejected.Error.Code, nonRetryable: true),
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
