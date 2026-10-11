using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>The YAML recipe reader (spec §3.1). Stub: P1 unit (a) implements it.</summary>
internal sealed class YamlRecipeReader : IRecipeReader
{
    public RecipeReading Read(string text, string file) => throw new NotSupportedException("the YAML recipe reader lands with P1 unit (a)");
}
