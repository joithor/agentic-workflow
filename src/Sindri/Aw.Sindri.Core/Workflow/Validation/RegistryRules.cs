using System.Globalization;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>
/// The rules that read the registry (spec §3.2 'version', §4.3, §9.1, W9): RCP-VERSION-001 (the same id and version is
/// registered with another hash), RCP-VERSION-002 (a new version isn't above the highest registered) and RCP-CAP-003 (a
/// generated recipe's base is a registered, approved hash that isn't revoked and whose pins match the build: invariant
/// 13's base, which Sindri code picks). They run only when the context carries <see cref="ValidationContext.Registry"/>.
/// RCP-CAP-003 runs again on the recipe itself at every Commit and dispatch (PinRules.Check), outside the validator.
/// </summary>
internal sealed class RegistryRules : IRecipeRules
{
    /// <inheritdoc/>
    public IEnumerable<RecipeFinding> Check(RecipeDocument document, ValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);
        if (context.Registry is not { } registry)
        {
            return [];
        }

        var findings = new List<RecipeFinding>();
        if (Version(document, registry) is { } version)
        {
            findings.Add(version);
        }

        if (context.BaseRecipe is not null && document.Recipe.Base is { } named && Base(named, registry, document.Source) is { } standing)
        {
            findings.Add(standing);
        }

        return findings;
    }

    /// <summary>RCP-VERSION-001, else RCP-VERSION-002, against the versions registered under the recipe's id.</summary>
    private static RecipeFinding? Version(RecipeDocument document, RecipeRegistryView registry)
    {
        var recipe = document.Recipe;
        var versions = registry.Registered.Where(r => r.Id == recipe.Id).ToList();
        if (versions.Count == 0)
        {
            return null;
        }

        var highest = versions.Max(r => r.Version);
        var next = Inv(highest + 1);
        if (versions.Exists(r => r.Version == recipe.Version && r.Hash == document.Hash))
        {
            return null;   // the registered recipe itself, validated again
        }

        if (versions.Find(r => r.Version == recipe.Version) is { } taken)
        {
            return document.Source.Finding("RCP-VERSION-001", "version",
                $"{recipe.Id.Value} version {Inv(recipe.Version)} is registered with hash {taken.Hash.Display}, but this recipe's hash is {document.Hash.Display}",
                $"raise version to {next}, one above the highest registered, or restore the content registered as version {Inv(recipe.Version)}");
        }

        return recipe.Version < highest
            ? document.Source.Finding("RCP-VERSION-002", "version",
                $"{recipe.Id.Value} version {Inv(recipe.Version)} isn't registered and is below version {Inv(highest)}, the highest registered",
                $"raise version to {next}, one above the highest registered")
            : null;
    }

    /// <summary>RCP-CAP-003 on a generated recipe's base: registered, approved, not revoked, its pins matching the build.</summary>
    private static RecipeFinding? Base(RecipeRef named, RecipeRegistryView registry, RecipeSource source)
    {
        var label = $"base {named.Id.Value}@{named.Hash.Display}";
        var approve = $"sindri recipe approve {named.Id.Value}@{named.Hash.Display}";
        if (!registry.Registered.Any(r => r.Hash == named.Hash))
        {
            return Finding($"{label} isn't registered", "generate the recipe from a registered, approved base");
        }

        return registry.Approvals.TryGetValue(named.Hash, out var standing)
            ? standing switch
            {
                RegistryApproval.Holds => null,
                RegistryApproval.Revoked => Finding($"{label} is revoked", "generate the recipe from an approved base, since a revoked hash can't be approved again"),
                RegistryApproval.Drifted => Finding($"{label} was approved against other block descriptors than this build's",
                    $"re-approve the base at a terminal ({approve}), then generate the recipe again"),
                _ => throw new ArgumentOutOfRangeException(nameof(registry), standing, "Not a registry approval."),
            }
            : Finding($"{label} is registered but not approved", $"approve the base at a terminal ({approve}), or generate the recipe from an approved base");

        RecipeFinding Finding(string message, string fix) => source.Finding("RCP-CAP-003", "base", message, fix);
    }

    private static string Inv(int value) => value.ToString(CultureInfo.InvariantCulture);
}
