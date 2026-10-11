namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>
/// The schema rules that need the bound long form (spec §9.1 "Schema", "Hash"): RCP-STATE-001, RCP-INIT-001, RCP-DUR-001 and RCP-HASH-001. RCP-SCHEMA-001–003 come from the reader (IRecipeReader), before binding.
/// Owned by P1 unit (a); a stub that reports nothing until that unit lands.
/// </summary>
internal sealed class SchemaRules : IRecipeRules
{
    public IEnumerable<RecipeFinding> Check(RecipeDocument document, ValidationContext context) => [];
}
