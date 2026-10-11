using System.Globalization;
using System.Text.Json.Serialization;

namespace Aw.Sindri.Features.EvalRecipe;

/// <summary>One eval case's result (spec §9.2).</summary>
/// <param name="Case">The case file, as the recipe lists it.</param>
/// <param name="Passed">Whether every line reached its expected state.</param>
/// <param name="Line">The first line that diverged; null when it passed.</param>
/// <param name="Detail">What diverged; null when it passed.</param>
public sealed record EvalCaseResult(string Case, bool Passed, int? Line, string? Detail)
{
    /// <summary>The case's name: its file name without '.jsonl' ('happy').</summary>
    [JsonIgnore]
    public string Name => Path.GetFileNameWithoutExtension(Case);

    /// <summary>The line 'recipe eval' prints: '&lt;name&gt; pass', or '&lt;name&gt; fail at line &lt;n&gt;: &lt;detail&gt;'.</summary>
    /// <returns>The line.</returns>
    public string ToLine() => Passed
        ? Name + " pass"
        : string.Create(CultureInfo.InvariantCulture, $"{Name} fail at line {Line}: {Detail}");
}
