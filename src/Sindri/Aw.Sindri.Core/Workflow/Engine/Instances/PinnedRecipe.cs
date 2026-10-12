using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Instances;

/// <summary>The recipe an instance runs (spec §5.1, W11): its id, version and hash, and the child hashes the approval pinned; a child is created from its pinned hash.</summary>
/// <param name="Id">The recipe's id.</param>
/// <param name="Version">Its version.</param>
/// <param name="Hash">Its approved hash.</param>
/// <param name="Children">The pinned child hash per child recipe.</param>
public sealed record PinnedRecipe(RecipeId Id, int Version, RecipeHash Hash, IReadOnlyDictionary<RecipeId, RecipeHash> Children);
