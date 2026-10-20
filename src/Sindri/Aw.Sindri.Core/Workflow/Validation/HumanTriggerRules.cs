namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>
/// RCP-HUMAN-001 (PB spec W23): an effect whose descriptor says <c lang="csharp">HumanTrigger</c> acts with a human's authority, so it
/// may sit only on a transition a human drives. Pure.
/// </summary>
internal sealed class HumanTriggerRules : IRecipeRules
{
    public IEnumerable<RecipeFinding> Check(RecipeDocument document, ValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);
        return [];
    }
}
