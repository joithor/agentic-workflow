using System.Security.Cryptography;
using System.Text;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The recipe interpreter: the one workflow type that runs every recipe (engine spec rev 8 §5.1). Its input is
/// <see cref="InstanceStart"/> and its workflow id is the instance address. Workflow code is deterministic: no I/O,
/// clock or random except through 'Workflow.*'; every decision is P3's pure <see cref="TransitionDecision"/>, and it
/// awaits with ConfigureAwait(true) (finding F3).
/// <para>
/// Engine P4. It loads the pinned recipe through 'LoadRecipe', enters the initial state, and loops (unit (a),
/// 'RecipeWorkflow.Loop.cs'): an active state runs its desk Step (a short 'RunStep' activity with the run token in
/// its id, then a wait for the 'complete' Update; rev 8.1 §5.3, unit (c)); a wait state, a park, an escalation or a
/// takeover waits for a delivered event that fits or a command an Update submits. Every trigger is decided at the wait,
/// one at a time; each change is mirrored into the ledger through 'RecordEvent' (§5.6) and the search attributes are
/// set. The other Updates of §5.2 have their final signatures and hand their trigger to the loop through 'Submit':
/// (b) 'answer', 'command', 'cancel' (and P5 'takeover', 'handback'); (c) 'complete' and its validator; (d) the bridge's
/// 'event' signal (§5.4).
/// </para>
/// </summary>
[Workflow("RecipeWorkflow")]
internal sealed partial class RecipeWorkflow
{
    private const string Desk = "human:desk";
    private const string Timers = "service:workflow.timers";
    private static readonly FactKey _itemFact = FactKey.From("fact:item");
    private readonly Queue<WaitSatisfied> _delivered = new();
    private readonly HashSet<long> _eventsTaken = [];
    private readonly Dictionary<string, Reported> _reported = new(StringComparer.Ordinal);
    private InstanceStart? _start;
    private InterpretedRecipe? _recipe;
    private Interpreting? _context;
    private WorkflowInstance? _instance;
    private DateTimeOffset _since;
    private DateTimeOffset? _timeoutAt;
    private TimeSpan _deskTimeout;
    private TimeSpan _waited;
    private string? _repo;
    private string? _recorded;
    private StepCompleted? _report;
    private string? _reportId;
    private int _runs;
    private int _mirrored;

    /// <summary>
    /// Interprets the recipe from its initial state to a terminal one (spec §5.1): an active state runs its Step (unit
    /// (c)'s desk seam, <see cref="RunDeskStep"/>); a wait state, a park, an escalation or a takeover waits
    /// (<see cref="Wait"/>). Each step decides one trigger with P3's pure <see cref="TransitionDecision"/>. Before it
    /// returns it lets the Update that ended the instance finish.
    /// </summary>
    /// <param name="start">The recipe, item, attempt, input facts, tenant and ring.</param>
    /// <returns>The terminal state.</returns>
    [WorkflowRun]
    public async Task<RecipeResult> RunAsync(InstanceStart start)
    {
        var (instance, context) = await Begin(start).ConfigureAwait(true);
        while (!instance.Ended)
        {
            instance = instance is { Suspension: null, Frame.Run: { } open }
                ? await RunDeskStep(instance, open, start, context).ConfigureAwait(true)
                : await Wait(instance, context).ConfigureAwait(true);
        }

        await Workflow.WaitConditionAsync(static () => Workflow.AllHandlersFinished).ConfigureAwait(true);   // the Update that ended it returns its result
        return new RecipeResult(instance.State, instance.Outcome?.Value);
    }

    /// <summary>
    /// Loads the pinned recipe ('LoadRecipe'), reads the repo 'fact:item' names ('ReadItemRepo'), enters the initial
    /// state with the input's tenant and ring, mirrors 'workflow.started' into the ledger and sets the search attributes.
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
        _deskTimeout = loaded.DeskTimeout;
        var decisions = new DecisionContext(ApprovalCheck.Holds, catalog, new HashSet<BlockId>(), NoChildRecipes.Instance);
        _context = new Interpreting(loaded.Recipe, decisions);
        _repo = start.Inputs.TryGetValue(_itemFact, out var item) ? await RepoOf(item.Value).ConfigureAwait(true) : null;
        var instance = Started(TransitionDecision.Start(StartOf(start, loaded), loaded.Recipe, decisions));
        Enter(instance);
        await Mirror(new WorkflowEventRecord(start.WorkflowId().Value, EventTypes.WorkflowStarted, start.Item.Value, string.Empty, instance.State, "start", Outcome: null, Run: null), NeedsYou(instance)).ConfigureAwait(true);
        return (instance, _context);
    }

    /// <summary>The instance as status shows it (the 'GetInstance' Query, spec §5.1, §6.3).</summary>
    /// <returns>The state, 'next', 'allowedCommands', the open question or desk task, the run token and the children.</returns>
    [WorkflowQuery("GetInstance")]
    public InstanceView GetInstance()
    {
        var start = _start ?? throw new InvalidOperationException("the instance has no input yet");
        return (_instance, _recipe) is ({ } instance, { } recipe)
            ? InstanceViews.Of(instance, recipe, _since, _timeoutAt) with { Version = _mirrored }
            : new InstanceView(start.WorkflowId(), start.Recipe, "starting", Detail: null, _since, string.Empty, [], Question: null, Task: null, Run: null, Children: [], Outcome: null);
    }

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
    /// wait; unit (a) consumes <see cref="_delivered"/> at a wait. Unit (d): the event's ledger sequence is its idempotency
    /// key, so a re-sent event (a bridge that crashed before it moved its cursor) is dropped; so is a kind no wait of the
    /// recipe waits for (the bridge signals every live instance of the item). Dropping one older than 'eventHold' is open
    /// (S1-D11's key, unit (e)).
    /// </summary>
    /// <param name="delivered">The event as the wait's trigger.</param>
    /// <returns>Done.</returns>
    [WorkflowSignal("event")]
    public Task EventAsync(WaitSatisfied delivered)
    {
        ArgumentNullException.ThrowIfNull(delivered);
        if (delivered.Source is EventWaitSource e && (!_eventsTaken.Add(e.Sequence) || !WaitsFor(e.Kind)))
        {
            return Task.CompletedTask;
        }

        _delivered.Enqueue(delivered);
        return Task.CompletedTask;
    }

    /// <summary>Whether a wait state of the recipe waits for <paramref name="kind"/>; true while the recipe isn't loaded yet.</summary>
    private bool WaitsFor(EventKind kind) =>
        _context is not { } context || context.Recipe.States.Values.Any(s => s.WaitFor?.Event == kind);

    /// <summary>
    /// Mirrors one event into the ledger ('RecordEvent', a local activity keyed by its own activity id; rev 8.1 §5.6),
    /// then sets the search attributes the instance is found by (§5.1) as they stand after it.
    /// </summary>
    private async Task Mirror(WorkflowEventRecord record, bool needsYou)
    {
        _ = await Workflow.ExecuteLocalActivityAsync((RecordEventActivities a) => a.RecordAsync(record), RecordEventActivities.LocalOptions).ConfigureAwait(true);
        _mirrored++;   // the instance's version, 'v<n>' in status: Temporal keeps no row version (unit (e))
        Upsert(record.To, needsYou);
    }

    /// <summary>
    /// The instance waits on a human (spec §5.1 'NeedsYou'): it is parked or escalated, or it holds an open desk task. A
    /// takeover doesn't: the human already has it.
    /// </summary>
    private static bool NeedsYou(WorkflowInstance instance) => !instance.Ended && instance.Suspension switch
    {
        { Kind: SuspensionKind.Takeover } => false,
        not null => true,
        null => instance.Frame.Run is { Runtime: StepRuntime.Desk },
    };

    private void Enter(WorkflowInstance instance)
    {
        if (_instance is null || !string.Equals(_instance.State, instance.State, StringComparison.Ordinal) || _instance.Frame.Entry != instance.Frame.Entry)
        {
            _since = Now;
        }

        if (_instance?.Suspension is not null && instance.Suspension is null)
        {
            _timeoutAt = null;   // an answer ended the suspension: its resume re-arms the desk deadline from now (§5.8 'rearm'; unit (b))
        }

        _instance = instance;
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
            TakenOver: false,
            start.Tenant,
            start.Ring);
    }

    private static WorkflowInstance Started(Decision decision) => decision switch
    {
        Applied applied => applied.Changes[0].After,
        Rejected rejected => throw new ApplicationFailureException($"the start was rejected: {rejected.Error.Code} {rejected.Error.Message}", errorType: rejected.Error.Code, nonRetryable: true),
        _ => throw new ApplicationFailureException($"the start was {decision.GetType().Name}", nonRetryable: true),
    };

    /// <summary>The instance after a decision: an applied or refused trigger's own change; a rejected or absorbed one leaves it as it was.</summary>
    private static WorkflowInstance After(WorkflowInstance instance, Decision decision) => OwnChange(instance, decision)?.After ?? instance;
}
