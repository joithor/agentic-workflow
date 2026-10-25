using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.ApproveAll;

/// <summary>A recipe approve-all left alone because a recipe it waits on was refused or skipped.</summary>
/// <param name="Id">The recipe's id.</param>
/// <param name="DisplayName">The name shown instead of the id.</param>
/// <param name="Because">The recipe it waits on.</param>
public sealed record ApproveAllSkipped(RecipeId Id, string DisplayName, RecipeId Because);
