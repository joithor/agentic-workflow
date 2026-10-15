using Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// What the 'GetInstance' Query returns (engine spec rev 8 §5.1 "Queries", §6.3 'GET /workflow/instances/{id}'): the
/// state, the rendered 'next', 'allowedCommands' as '{ actionId, cli }' (UI E-13), the open question or desk task with
/// the Step's declared outcomes and outputs, the run token and the children. 'workflow status' renders it (§6.4).
/// </summary>
/// <param name="Address">The instance address (its workflow id).</param>
/// <param name="Recipe">The recipe it runs.</param>
/// <param name="State">The state status shows: a recipe state, or 'parked', 'escalated', 'taken-over', 'starting'.</param>
/// <param name="Detail">What follows the state in the header ('desk task', a park reason), when there is one.</param>
/// <param name="Since">When the instance entered <see cref="State"/>.</param>
/// <param name="Next">The rendered 'next' text.</param>
/// <param name="AllowedCommands">The commands it accepts now, the instance's own verbs first (§6.4 order).</param>
/// <param name="Question">The open park, escalation or takeover.</param>
/// <param name="Task">The open desk task.</param>
/// <param name="Run">The open run's token.</param>
/// <param name="Children">The live children.</param>
/// <param name="Outcome">The terminal outcome, once it ended.</param>
public sealed record InstanceView(
    InstanceAddress Address,
    RecipeVersion Recipe,
    string State,
    string? Detail,
    DateTimeOffset Since,
    string Next,
    IReadOnlyList<AllowedCommand> AllowedCommands,
    OpenQuestion? Question,
    DeskTask? Task,
    RunToken? Run,
    IReadOnlyList<ChildView> Children,
    OutcomeName? Outcome)
{
    /// <summary>
    /// The instance's opaque id (spec §6.3), minted from <see cref="Address"/> with <see cref="WorkflowInstanceId.Of"/>:
    /// what the API's instance routes take. Required, so a view read back from JSON always carries it (ARCH-ID-002).
    /// </summary>
    public required WorkflowInstanceId Id { get; init; }

    /// <summary>
    /// The instance's version, 'v&lt;n&gt;' in the status header: the count of events the interpreter has mirrored into the
    /// ledger (the start and every transition). Temporal keeps no row version, so the mirror count stands in for it.
    /// </summary>
    public int Version { get; init; }

    /// <summary>The suggested answer line for a park or escalation (§6.4, one per park reason); null when none applies.</summary>
    public string? Answer { get; init; }

    /// <summary>A drift recorded behind the open park (§6.4 'drift'): the block, and that the answer needs the re-approval first.</summary>
    public string? Drift { get; init; }

    /// <summary>The token budgets of the active state and the instance, when the recipe declares one (§6.4 'budget').</summary>
    public BudgetView? Budget { get; init; }

    /// <summary>What was left of the state's timeout when a takeover paused it (§6.4 'paused'; engine P5 sets it).</summary>
    public TimeSpan? Paused { get; init; }

    /// <summary>The item's id ('ITM-...') when the instance is an item's; null otherwise (UI screens E-20).</summary>
    public string? ItemId { get; init; }

    /// <summary>How soon the recipe's instances need a human (E-21); <see cref="Urgency.Work"/> unless the recipe says so.</summary>
    public Urgency Urgency { get; init; } = Urgency.Work;

    /// <summary>The badge that replaces the state's glyph when the engine interrupted the instance; null otherwise.</summary>
    public EngineBadge? EngineOwned { get; init; }

    /// <summary>What the instance is about, from the recipe's first input; null when the recipe has no input.</summary>
    public SubjectRef? Subject { get; init; }

    /// <summary>The recipe's inputs, in declaration order.</summary>
    public IReadOnlyList<InputSlot> Inputs { get; init; } = [];

    /// <summary>How <see cref="States"/> is ordered: the path, or the transition list when the graph has several initial paths.</summary>
    public OrderedBy OrderedBy { get; init; } = OrderedBy.Path;

    /// <summary>Every state of the recipe, in <see cref="OrderedBy"/> order.</summary>
    public IReadOnlyList<StateCell> States { get; init; } = [];

    /// <summary>
    /// The status layout as fields (E-23). Nullable because it has no empty value: it is null on a view the layout was
    /// not computed for.
    /// </summary>
    public StatusFields? Status { get; init; }

    /// <summary>One row per input and output the recipe declares, in declaration order.</summary>
    public IReadOnlyList<FactRow> Facts { get; init; } = [];

    /// <summary>The children, one group per state that declares child or runs step.children; empty until engine P7.</summary>
    public IReadOnlyList<ChildGroup> ChildGroups { get; init; } = [];
}
