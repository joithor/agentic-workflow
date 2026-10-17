using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Recipes;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The recipe sheet in the shell (<see cref="ShellViewModel"/>, continued; U10a): a recipe link opens it, read by id and by hash when the link names one.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The recipe sheet.</summary>
    public RecipeSheetViewModel Recipe { get; }

    /// <summary>The Workflows group whose Recipe button opened the sheet (its recipe id), where Esc returns focus; null when it was opened some other way.</summary>
    internal string? OpenerRecipeGroup { get; private set; }

    /// <summary>True when the sheet's subject is a recipe.</summary>
    public bool IsRecipeSheet => SheetLink?.Kind == DeepLinkKind.Recipe;

    // The recipe part of opening the sheet (OnSheetOpened): the sheet names its recipe at once and reads it.
    private void OnRecipeSheetOpened(DeepLink link)
    {
        Changed(nameof(IsRecipeSheet), nameof(ShownSheetWidth));
        if (link.Kind != DeepLinkKind.Recipe || link.Subject is not { } subject)
        {
            return;
        }

        Recipe.Show(subject);
        if (Health.ApiUrl() is { } url)
        {
            _ = Recipe.Open(url, subject, CancellationToken.None);
        }
    }

    private void InitRecipe()
    {
        Health.Follow((url, token) => IsRecipeSheet ? Recipe.Refresh(url, token) : Task.CompletedTask);

        // The frame's link stacks the recipe on it (Back returns to the frame); a group head's button opens the approved version over the page it is on.
        Instance.RecipeRequested += (_, e) =>
        {
            Stacked(() => OpenSheet(new DeepLink(DeepLinkKind.Recipe, e.Subject)), push: true);
            SheetStacked?.Invoke(this, EventArgs.Empty);   // the Recipe button stays under the sheet: focus goes to the sheet's heading
        };
        Workflows.RecipeRequested += (_, e) => OpenSheet(new DeepLink(DeepLinkKind.Recipe, e.Subject));
    }
}
