namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>What one outcome of a desk state means, in the recipe author's words.</summary>
/// <param name="Outcome">The outcome name.</param>
/// <param name="Text">What choosing it does.</param>
internal sealed record RecipeOutcomeText(string Outcome, string Text);
