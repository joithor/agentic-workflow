using Aw.Sindri.Core.Workflow.ApproveAll;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Testing.Workflow.ApproveAll;

/// <summary>One recipe a shipped set holds, as <see cref="ShippedRecipesContract"/> seeds it: what an implementation of the port must be able to be put in front of.</summary>
/// <param name="Id">The recipe's id.</param>
/// <param name="DisplayName">The name the app shows.</param>
/// <param name="Hash">The shipped hash.</param>
/// <param name="Children">The recipes it uses, shipped too.</param>
/// <param name="Capabilities">What it can do, in plain words.</param>
/// <param name="PreviousApprovedHash">The hash last approved, or null when never approved.</param>
/// <param name="Version">Its version.</param>
/// <param name="Refusal">What the approval checks refuse it with (the implementation arranges that its own way), or null when they pass.</param>
public sealed record ShippedSeed(RecipeId Id, string DisplayName, RecipeHash Hash, IReadOnlyList<RecipeId> Children, IReadOnlyList<CapabilityLine> Capabilities,
    RecipeHash? PreviousApprovedHash = null, int Version = 1, SeededRefusal? Refusal = null);
