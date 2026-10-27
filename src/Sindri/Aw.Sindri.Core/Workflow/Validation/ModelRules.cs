namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>SND-MODEL-002 (model-pick spec 8, 12): the floor of a state is cheap, standard or strong, never a model id.</summary>
internal sealed class ModelRules : IRecipeRules
{
    /// <inheritdoc/>
    public IEnumerable<RecipeFinding> Check(RecipeDocument document, ValidationContext context) => throw new NotSupportedException("SND-MODEL-002 comes with the green");
}
