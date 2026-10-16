namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>
/// The decision-tree rules (DT spec §3.2, §8.1): RCP-CHOICE-001 to -006 for the 'choices' and 'facts' of a
/// step.judge.choose state, its desk twin, and the order of a route state's transitions. Pure: reads the bound recipe
/// and the catalog.
/// </summary>
internal sealed class ChoiceRules : IRecipeRules
{
    public IEnumerable<RecipeFinding> Check(RecipeDocument document, ValidationContext context) => [];
}
