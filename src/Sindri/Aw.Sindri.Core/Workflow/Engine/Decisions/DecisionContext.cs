using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Core.Workflow.Engine.Decisions;

/// <summary>
/// What a decision reads besides the instance, the recipe and the trigger. Everything a decision would otherwise get
/// from I/O is here, so every decision is a pure function. Child ids and outbox command ids are derived from the
/// trigger key (UUIDv8 over SHA-256, spec §5.4), so a decision needs no id generator.
/// </summary>
/// <param name="Now">The time the trigger is decided at.</param>
/// <param name="Epoch">The lease epoch the Commit is written under.</param>
/// <param name="Approval">The approval check's answer for the instance's recipe.</param>
/// <param name="Catalog">The build's block and event descriptors (rungs, outcomes, outputs, deskCapable).</param>
/// <param name="Runtimes">The blocks and resources this build has a runtime for; a Step without one parks with SND-WF-010 unless it is deskCapable (W5).</param>
/// <param name="Children">The pinned child recipes, for a child wait's creation.</param>
/// <param name="Timings">The profile's engine timings.</param>
public sealed record DecisionContext(
    DateTimeOffset Now,
    long Epoch,
    ApprovalCheck Approval,
    BlockCatalog Catalog,
    IReadOnlySet<BlockId> Runtimes,
    IChildRecipeResolver Children,
    EngineTimings Timings);
