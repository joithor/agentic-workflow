using System.Text.Json.Serialization;

namespace Aw.Judge.Core.Config;

/// <summary>The prompt sorter's overrides in config.json (J2 reads them); each null is the sorter's default.</summary>
/// <param name="Enabled">Whether the sorter runs.</param>
/// <param name="BudgetMs">Its time budget, clamped to 200–1050 ms.</param>
/// <param name="CooldownPrompts">Prompts between two scaffolds, at least 0.</param>
/// <param name="Scaffolds">Per-scaffold switches, known scaffold ids only.</param>
public sealed record PromptSortConfig(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Enabled,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? BudgetMs,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? CooldownPrompts,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, bool>? Scaffolds);
