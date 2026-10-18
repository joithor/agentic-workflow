using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>A child recipe the approval pinned, as 'LoadChildren' read it (engine P7 plan D2).</summary>
/// <param name="Id">The child's recipe id.</param>
/// <param name="Hash">The hash the approval pinned.</param>
/// <param name="Recipe">Its long form, the hash set.</param>
internal sealed record LoadedChild(RecipeId Id, RecipeHash Hash, Recipe Recipe);
