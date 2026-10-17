using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Recipes;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The recipe sheet in the shell (<see cref="ShellViewModel"/>, continued; U10a): a recipe link opens it, read by id and by hash when the link names one.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The recipe sheet.</summary>
    public RecipeSheetViewModel Recipe { get; }

    /// <summary>True when the sheet's subject is a recipe.</summary>
    public bool IsRecipeSheet => SheetLink?.Kind == DeepLinkKind.Recipe;

    private void InitRecipe() => Health.Follow((url, token) => IsRecipeSheet ? Recipe.Refresh(url, token) : Task.CompletedTask);
}
