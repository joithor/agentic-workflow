using System.Text.Json;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>The YAML recipe reader (spec §3.1). Red stub: P1 unit (a) implements it.</summary>
internal sealed class YamlRecipeReader : IRecipeReader
{
    /// <summary>The largest recipe file read, in UTF-8 bytes (256 KiB).</summary>
    internal const int MaxBytes = 256 * 1024;

    public YamlRecipeReader(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
    }

    public RecipeReading Read(string text, string file) => throw new NotSupportedException("red: the YAML recipe reader lands with P1 unit (a)");
}
