using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>The frame's link to the recipe it runs (<see cref="InstanceFrameViewModel"/>, continued; U10a): it opens the version the instance is pinned to.</summary>
internal sealed partial class InstanceFrameViewModel
{
    /// <summary>Raised when the frame asks to open its recipe's sheet.</summary>
    internal event EventHandler<RecipeRequestedEventArgs>? RecipeRequested;

    /// <summary>The recipe link's subject: the recipe id and the first twelve digits of the hash the instance runs, "id@hash12"; empty before a read.</summary>
    public string RecipeSubject => Recipe.Length > 0 && Hash12.Length > 0 ? $"{Recipe}@{Hash12}" : string.Empty;

    /// <summary>True when the frame knows its recipe.</summary>
    public bool HasRecipeLink => RecipeSubject.Length > 0;

    /// <summary>The link's words.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string RecipeLinkText => Labels.FrameTypeLink;
#pragma warning restore CA1822, MA0041

    /// <summary>The link's accessible name: it leads with the visible word and names the workflow (the pinned version opens, but is for Details).</summary>
    public string RecipeLinkName => Labels.FrameTypeLinkName(WorkflowName);

    /// <summary>Opens the recipe sheet on the version this instance runs.</summary>
    internal void OpenRecipe()
    {
        if (HasRecipeLink)
        {
            RecipeRequested?.Invoke(this, new RecipeRequestedEventArgs(RecipeSubject));
        }
    }
}
