namespace Aw.Sindri.Ui.ViewModels.Start;

/// <summary>One approved recipe in the picker.</summary>
/// <param name="Id">The recipe id, which is what a start names (the approved latest).</param>
/// <param name="Version">The version shown.</param>
/// <param name="Display">The picker's line.</param>
internal sealed record RecipeChoice(string Id, int Version, string Display);
