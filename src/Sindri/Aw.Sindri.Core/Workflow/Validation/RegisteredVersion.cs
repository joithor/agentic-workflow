using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>One registered recipe's key (spec §3.2, §4.1): its id, version and hash.</summary>
/// <param name="Id">The recipe's id.</param>
/// <param name="Version">Its version.</param>
/// <param name="Hash">Its hash.</param>
public sealed record RegisteredVersion(RecipeId Id, int Version, RecipeHash Hash);
