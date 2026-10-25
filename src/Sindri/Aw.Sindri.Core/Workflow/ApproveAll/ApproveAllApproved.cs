using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.ApproveAll;

/// <summary>A recipe approve-all approved.</summary>
/// <param name="Id">The recipe's id.</param>
/// <param name="DisplayName">The name shown instead of the id.</param>
/// <param name="Hash">The hash approved.</param>
public sealed record ApproveAllApproved(RecipeId Id, string DisplayName, RecipeHash Hash);
