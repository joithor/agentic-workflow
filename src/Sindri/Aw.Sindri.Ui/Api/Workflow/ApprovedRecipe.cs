namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>An approved recipe.</summary>
/// <param name="Id">The recipe id.</param>
/// <param name="DisplayName">The name the app shows; optional.</param>
/// <param name="Hash">The hash approved.</param>
internal sealed record ApprovedRecipe(string Id, string? DisplayName, string Hash);
