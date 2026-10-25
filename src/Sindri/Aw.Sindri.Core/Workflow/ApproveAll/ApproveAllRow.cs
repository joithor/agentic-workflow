using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.ApproveAll;

/// <summary>One recipe the hold would approve (setup-once spec §7.2 'ApproveAllPlan.recipes').</summary>
/// <param name="Id">The recipe's id.</param>
/// <param name="DisplayName">The name shown instead of the id.</param>
/// <param name="Version">Its version.</param>
/// <param name="Hash">The hash that would be approved.</param>
/// <param name="PreviousApprovedHash">The hash last approved, or null when it was never approved.</param>
/// <param name="Change">What changed since.</param>
/// <param name="ChangeWord">The words for <paramref name="Change"/>.</param>
/// <param name="Capabilities">Everything the recipe can do, in plain words.</param>
/// <param name="Delta">The capability change since the last approved hash.</param>
/// <param name="ChangedBlocks">The ids of the blocks that changed since.</param>
/// <param name="Children">The recipes it uses, which are approved before it.</param>
public sealed record ApproveAllRow(
    RecipeId Id,
    string DisplayName,
    int Version,
    RecipeHash Hash,
    RecipeHash? PreviousApprovedHash,
    PlanChange Change,
    string ChangeWord,
    IReadOnlyList<CapabilityLine> Capabilities,
    CapabilityDelta Delta,
    IReadOnlyList<string> ChangedBlocks,
    IReadOnlyList<RecipeId> Children);
