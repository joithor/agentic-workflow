using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Registry;

/// <summary>A revoked hash (spec §4.1, W11): a revoke cascades to the generated recipes approved from it, each with its own row.</summary>
/// <param name="Id">The recipe's id.</param>
/// <param name="Version">Its version.</param>
/// <param name="Hash">The revoked hash.</param>
/// <param name="RevokedBy">The actor who revoked it (for a cascaded row, the actor of the revoke that caused it).</param>
/// <param name="RevokedAt">When.</param>
/// <param name="CascadedFrom">The base whose revoke cascaded to this one, or null for the revoke the human asked for.</param>
public sealed record RecipeRevocation(RecipeId Id, int Version, RecipeHash Hash, string RevokedBy, DateTimeOffset RevokedAt, RecipeHash? CascadedFrom);
