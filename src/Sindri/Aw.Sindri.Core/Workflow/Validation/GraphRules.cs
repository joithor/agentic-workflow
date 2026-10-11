namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>
/// The graph rules (spec §9.1): RCP-REACH-001 (reachable), RCP-EXIT-001 (a way out), RCP-TIME-001/002 (timeouts), RCP-LOOP-001–003 (loop caps), RCP-TRIG-001/002 (triggers), RCP-NEXT-001/002 and RCP-CHILD-002/003 (child cycles and depth).
/// Owned by P1 unit (b); a stub that reports nothing until that unit lands.
/// </summary>
internal sealed class GraphRules : IRecipeRules
{
    public IEnumerable<RecipeFinding> Check(RecipeDocument document, ValidationContext context) => [];
}
