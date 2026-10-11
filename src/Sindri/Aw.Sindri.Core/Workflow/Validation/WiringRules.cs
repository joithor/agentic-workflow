namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>
/// The wiring rules (spec §9.1 "Inputs upstream", W2: checked on every path): RCP-WIRE-001/002, RCP-WHEN-001/002, RCP-OUTCOME-001 and RCP-CHILD-001.
/// Owned by P1 unit (c); a stub that reports nothing until that unit lands.
/// </summary>
internal sealed class WiringRules : IRecipeRules
{
    public IEnumerable<RecipeFinding> Check(RecipeDocument document, ValidationContext context) => [];
}
