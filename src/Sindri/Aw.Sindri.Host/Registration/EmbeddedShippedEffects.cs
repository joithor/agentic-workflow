using System.Text;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Shipped;

namespace Aw.Sindri.Host.Registration;

/// <summary>
/// <see cref="IShippedEffects"/> over the recipes embedded in this build (setup-once spec §5.1, §5.3): the union of the effects their transitions emit, read once. A build that embeds none ships no effect.
/// A file that does not bind is left out; the engine's registration reports it.
/// </summary>
/// <param name="embedded">The embedded recipe set.</param>
/// <param name="reader">Reads a recipe's text.</param>
internal sealed class EmbeddedShippedEffects(IEmbeddedRecipes embedded, IRecipeReader reader) : IShippedEffects
{
    private readonly Lazy<IReadOnlyList<string>> _kinds = new(() => Read(embedded, reader), LazyThreadSafetyMode.ExecutionAndPublication);

    public IReadOnlyList<string> Kinds() => _kinds.Value;

    private static IReadOnlyList<string> Read(IEmbeddedRecipes embedded, IRecipeReader reader)
    {
        List<Recipe> recipes = [];
        foreach (var file in embedded.Files().Where(static f => !f.RelativePath.Contains('/', StringComparison.Ordinal) && f.RelativePath.EndsWith(".yaml", StringComparison.Ordinal)))
        {
            if (reader.Read(Encoding.UTF8.GetString(file.Bytes.Span), file.RelativePath).Document is { } document)
            {
                recipes.Add(document.Recipe);
            }
        }

        return ShippedEffects.Union(recipes);
    }
}
