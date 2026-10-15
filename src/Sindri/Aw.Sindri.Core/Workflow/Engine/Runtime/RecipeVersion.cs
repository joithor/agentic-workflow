using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>The recipe an instance runs, as its workflow input names it (engine spec rev 8 §5.1): its id, version and approved hash.</summary>
/// <param name="Id">The recipe's id.</param>
/// <param name="Version">Its version.</param>
/// <param name="Hash">The approved hash the instance is pinned to.</param>
public sealed record RecipeVersion(RecipeId Id, int Version, RecipeHash Hash);
