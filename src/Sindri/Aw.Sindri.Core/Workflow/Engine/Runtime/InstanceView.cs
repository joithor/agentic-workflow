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
}
