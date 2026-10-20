using System.Reflection;
using Aw.Sindri.Core.Workflow.Shipped;

namespace Aw.Sindri.Host.Shipped;

/// <summary>
/// The embedded recipes (setup-once spec §5.3): the manifest resources of <paramref name="assembly"/> named <c language="text">aw-recipes/sindri/&lt;relative path&gt;</c>. The publish stages
/// <c language="text">git archive HEAD recipes/sindri</c> and an MSBuild target in this project embeds it under that prefix (SO-A7a); until it lands, and in a build that stages nothing, the set is empty.
/// </summary>
/// <param name="assembly">The assembly that holds the resources.</param>
internal sealed class ManifestEmbeddedRecipes(Assembly assembly) : IEmbeddedRecipes
{
    /// <summary>The resource name prefix.</summary>
    internal const string Prefix = "aw-recipes/sindri/";

    public IReadOnlyList<RecipeSetFile> Files()
    {
        _ = assembly;
        throw new NotSupportedException("Not built yet.");
    }
}
