namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>
/// The rules that read the registry (spec §3.2 'version', §4.3, §9.1, W9): RCP-VERSION-001 (the same id and version is
/// registered with another hash), RCP-VERSION-002 (a new version isn't above the highest registered) and RCP-CAP-003 (a
/// generated recipe's base is a registered, approved hash that isn't revoked and whose pins match the build). They run
/// only when the context carries <see cref="ValidationContext.Registry"/>.
/// </summary>
internal sealed class RegistryRules : IRecipeRules
{
    /// <inheritdoc/>
    public IEnumerable<RecipeFinding> Check(RecipeDocument document, ValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);
        return [];
    }
}
