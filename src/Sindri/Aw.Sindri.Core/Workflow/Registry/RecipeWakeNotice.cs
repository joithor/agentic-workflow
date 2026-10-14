using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Registry;

/// <summary>A registry change the running instances must see at once (spec §4.1, W11): a revoke, a drift, or a drift approval's resume.</summary>
/// <param name="Hash">The recipe hash the change is about (a parent pinning a revoked child is reached through <see cref="IRecipeApprovals.Pinning"/>).</param>
/// <param name="Kind">Revoked, drifted or approved.</param>
/// <param name="Sequence">The registry event's ledger sequence: the wake trigger is keyed '&lt;kind&gt;:{seq}'.</param>
/// <param name="OnBehalfOf">The human actor the 'service:engine' trigger acts for.</param>
public sealed record RecipeWakeNotice(RecipeHash Hash, RecipeWakeKind Kind, long Sequence, string OnBehalfOf);
