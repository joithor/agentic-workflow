namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>
/// The capability rules (spec §4.3, W10): RCP-CAP-001/002 and RCP-CAP-004–007 keep a generated recipe at or below its base (ValidationContext.BaseRecipe), comparing guards by path and loops by state. RCP-CAP-003 runs at Commit and dispatch, not here (§4.3).
/// Owned by P1 unit (d); a stub that reports nothing until that unit lands.
/// </summary>
internal sealed class CapabilityRules : IRecipeRules
{
    public IEnumerable<RecipeFinding> Check(RecipeDocument document, ValidationContext context) => [];
}
