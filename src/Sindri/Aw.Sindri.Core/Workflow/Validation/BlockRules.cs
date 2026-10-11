namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>
/// The block and event rules (spec §9.1 "Blocks registered", "Allowlisted effects"): RCP-BLOCK-001–003, RCP-EVID-001, RCP-EVENT-001, RCP-THRESH-001/002, RCP-EFFECT-001 (skipped without the profile's allowlist) and RCP-RES-001 (information).
/// Owned by P1 unit (c); a stub that reports nothing until that unit lands.
/// </summary>
internal sealed class BlockRules : IRecipeRules
{
    public IEnumerable<RecipeFinding> Check(RecipeDocument document, ValidationContext context) => [];
}
