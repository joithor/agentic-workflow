using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Core.Workflow.RegistryLedger;

/// <summary>A recorded revoke: the revocation, the cascaded ones, and the ledger sequence of the revoked hash's 'recipe.revoked'.</summary>
/// <param name="Revocation">The revocation the human asked for.</param>
/// <param name="Cascade">The generated recipes' revocations it cascaded to.</param>
/// <param name="Sequence">The revoked hash's event sequence.</param>
public sealed record RevokeWritten(RecipeRevocation Revocation, IReadOnlyList<RecipeRevocation> Cascade, long Sequence);
