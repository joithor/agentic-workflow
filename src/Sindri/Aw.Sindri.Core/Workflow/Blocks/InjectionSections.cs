namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>Sections of a Step's injection plan (spec §2, A §17.8), by section name.</summary>
/// <param name="Sections">The text of each section.</param>
/// <param name="Result">The input's typed result for a Step that reads it (W6); null when it returns none.</param>
public sealed record InjectionSections(IReadOnlyDictionary<string, string> Sections, System.Text.Json.JsonElement? Result = null)
{
    /// <summary>No sections.</summary>
    public static InjectionSections Empty { get; } = new(new Dictionary<string, string>(StringComparer.Ordinal));
}
