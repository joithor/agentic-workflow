using System.Text.Json;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>The choice grammar (DT spec §3.2): "&lt;outcome&gt;: &lt;criterion&gt;", split at the first ": ".</summary>
public static class ChoiceList
{
    /// <summary>The longest criterion.</summary>
    public const int MaxCriterion = 400;

    /// <summary>Parses a literal list value.</summary>
    /// <param name="list">The literal; a value that isn't an array gives no entries and no faults.</param>
    /// <returns>The entries and faults.</returns>
    public static ChoiceParse Parse(JsonElement list) => throw new NotSupportedException("ChoiceList.Parse is not implemented yet");
}
