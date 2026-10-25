using System.Reflection;
using Aw.Sindri.Core.Workflow.Shipped;

namespace Aw.Sindri.Host.Shipped;

/// <summary>
/// The embedded recipes (setup-once spec §5.3): the manifest resources of <paramref name="assembly"/> named <c language="text">aw-recipes/sindri/&lt;relative path&gt;</c>. The publish stages
/// <c language="text">git archive HEAD recipes/sindri</c> and an MSBuild target in this project embeds it under that prefix (SO-A7a); until it lands, and in a build that stages nothing, the set is empty.
/// A resource under the prefix that is not a flat recipe or an eval case (the README) is left out.
/// </summary>
/// <param name="assembly">The assembly that holds the resources.</param>
internal sealed class ManifestEmbeddedRecipes(Assembly assembly) : IEmbeddedRecipes
{
    /// <summary>The resource name prefix.</summary>
    internal const string Prefix = "aw-recipes/sindri/";

    private readonly Lazy<IReadOnlyList<RecipeSetFile>> _files = new(() => Load(assembly));

    public IReadOnlyList<RecipeSetFile> Files() => _files.Value;

    private static List<RecipeSetFile> Load(Assembly assembly)
    {
        var files = new List<RecipeSetFile>();
        foreach (var name in assembly.GetManifestResourceNames().Where(static n => n.StartsWith(Prefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            var relative = name[Prefix.Length..];
            if (!IsRecipeOrEval(relative))
            {
                continue;
            }

            using var stream = assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException($"The embedded resource {name} listed but cannot be opened.");
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            files.Add(new RecipeSetFile(relative, bytes.ToArray()));
        }

        return files;
    }

    private static bool IsRecipeOrEval(string relative) => relative.Split('/') switch
    {
        [var flat] => flat.EndsWith(".yaml", StringComparison.Ordinal),
        ["evals", { Length: > 0 }, var name] => name.EndsWith(".jsonl", StringComparison.Ordinal),
        _ => false,
    };
}
