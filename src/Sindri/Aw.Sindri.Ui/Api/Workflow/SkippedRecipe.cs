namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>A recipe left because one it waits on was refused or skipped.</summary>
/// <param name="Id">The recipe id.</param>
/// <param name="DisplayName">The name the app shows; optional.</param>
/// <param name="Because">The recipe it waits on.</param>
internal sealed record SkippedRecipe(string Id, string? DisplayName, string Because);
