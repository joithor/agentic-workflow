using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Core.Workflow.Engine.Decisions;

/// <summary>
/// What a decision reads besides the instance, the recipe and the trigger (spec §5.1). Everything a decision would
/// otherwise get from I/O is here, so every decision is a pure function the interpreter can call from workflow code;
/// the interpreter fills it from activities. Time is the interpreter's: its timers fire triggers (§5.5), and no
/// decision reads a clock.
/// </summary>
/// <param name="Approval">The approval check's answer for the instance's recipe.</param>
/// <param name="Catalog">The build's block and event descriptors (rungs, outcomes, outputs, deskCapable).</param>
/// <param name="Runtimes">The blocks and resources this build has a runtime for; a Step without one parks with SND-WF-010 unless it is deskCapable (W5).</param>
/// <param name="Children">The pinned child recipes, for a child wait's creation.</param>
public sealed record DecisionContext(
    ApprovalCheck Approval,
    BlockCatalog Catalog,
    IReadOnlySet<BlockId> Runtimes,
    IChildRecipeResolver Children)
{
    /// <summary>
    /// The profile's 'workflow.deskTimeout' (W20): a desk Step's timer runs at least this long, and the park its expiry
    /// leaves names the time the Step had.
    /// </summary>
    public TimeSpan DeskTimeout { get; init; } = DeskTimeouts.DefaultFloor;
}
