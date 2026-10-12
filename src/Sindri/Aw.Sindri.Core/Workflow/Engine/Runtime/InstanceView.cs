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
    OutcomeName? Outcome);
