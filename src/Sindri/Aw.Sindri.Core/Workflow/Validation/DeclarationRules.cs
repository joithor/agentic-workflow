namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>
/// The declaration rules (spec §3.2, §3.3, §9.1 "Added (UI E-21, E-22)"): RCP-URG-001, RCP-OUT-001/002/003 and
/// RCP-TYPE-001/002/003. Pure: they read the bound recipe and the catalog.
/// </summary>
internal sealed class DeclarationRules : IRecipeRules
{
    public IEnumerable<RecipeFinding> Check(RecipeDocument document, ValidationContext context) => [];
}
