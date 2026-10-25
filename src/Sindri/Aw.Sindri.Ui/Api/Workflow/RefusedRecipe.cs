namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>A recipe the checks refused.</summary>
/// <param name="Id">The recipe id.</param>
/// <param name="Hash">The hash that was refused.</param>
/// <param name="Code">The refusal's code.</param>
/// <param name="Sentence">The refusal in words.</param>
/// <param name="Cli">The command that approves it on its own.</param>
internal sealed record RefusedRecipe(string Id, string Hash, string Code, string Sentence, string? Cli);
