namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>The plain-words rules (setup-once spec §5.3): RCP-DISPLAY-001 (a recipe's displayName), -002 (a state's why) and -003 (a state's displayName and a transition's label).</summary>
internal sealed class DisplayRules : IRecipeRules
{
    /// <inheritdoc/>
    public IEnumerable<RecipeFinding> Check(RecipeDocument document, ValidationContext context)
    {
        _ = PlainWords.MaxName;   // the rules arrive with the green commit
        return [];
    }
}
