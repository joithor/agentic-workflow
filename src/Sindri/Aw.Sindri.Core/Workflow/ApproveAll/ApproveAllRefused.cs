using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.ApproveAll;

/// <summary>A recipe the approval checks refused; the sentence carries no command, the fix is in <paramref name="Cli"/>.</summary>
/// <param name="Id">The recipe's id.</param>
/// <param name="Hash">The hash that was refused.</param>
/// <param name="Code">The error code, for example SND-RECIPE-009.</param>
/// <param name="Sentence">The plain sentence.</param>
/// <param name="Cli">The one-recipe approval to run instead (for example 'sindri recipe approve &lt;id&gt;'), or null when the check has none of its own.</param>
public sealed record ApproveAllRefused(RecipeId Id, RecipeHash Hash, string Code, string Sentence, string? Cli);
