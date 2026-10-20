namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>One recipe the plan covers.</summary>
/// <param name="Id">The recipe id.</param>
/// <param name="DisplayName">The name the app shows; read as optional (an older Sindri serves none).</param>
/// <param name="Version">The recipe version.</param>
/// <param name="Hash">The content hash.</param>
/// <param name="PreviousApprovedHash">The hash approved before; null for a never-approved recipe.</param>
/// <param name="Change">'new', 'updated' or 'second-look'.</param>
/// <param name="ChangeWord">The change in a word: New, Updated, Needs a second look.</param>
/// <param name="Capabilities">What the recipe can do, in plain words.</param>
/// <param name="Delta">What changed since it was last approved.</param>
/// <param name="ChangedBlocks">The blocks that changed (ids).</param>
/// <param name="Children">The recipes it waits on (ids).</param>
internal sealed record PlanRecipe(
    string Id,
    string? DisplayName,
    int Version,
    string Hash,
    string? PreviousApprovedHash,
    string Change,
    string ChangeWord,
    IReadOnlyList<CapabilityLine> Capabilities,
    CapabilityDelta? Delta,
    IReadOnlyList<string> ChangedBlocks,
    IReadOnlyList<string> Children);
