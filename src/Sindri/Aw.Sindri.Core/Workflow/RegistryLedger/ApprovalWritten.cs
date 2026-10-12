using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Core.Workflow.RegistryLedger;

/// <summary>A recorded approval: the stored row and the ledger sequence of its 'recipe.approved'.</summary>
/// <param name="Approval">The stored row.</param>
/// <param name="Sequence">The event's ledger sequence.</param>
public sealed record ApprovalWritten(RecipeApproval Approval, long Sequence);
