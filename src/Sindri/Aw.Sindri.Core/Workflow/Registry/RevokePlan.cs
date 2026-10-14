using Aw.Sindri.Core.Challenges;

namespace Aw.Sindri.Core.Workflow.Registry;

/// <summary>What revoking an approved hash would do (engine spec §4.1, §4.2, W11): the preview, the approvals the cascade revokes, and the challenge.</summary>
/// <param name="Recipe">The registered recipe.</param>
/// <param name="Preview">The preview the human reads.</param>
/// <param name="Cascade">The generated recipes' approvals the cascade revokes, nearest base first.</param>
/// <param name="Action">'revoke &lt;id&gt;@&lt;hash12&gt;'.</param>
/// <param name="Subject">The recipe hash and version.</param>
public sealed record RevokePlan(RegisteredRecipe Recipe, RevokePreview Preview, IReadOnlyList<RecipeApproval> Cascade, ChallengeAction Action, ChallengeSubject Subject);
