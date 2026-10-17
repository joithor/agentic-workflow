using System.Text.Json;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>The choice grammar (DT spec §3.2): "&lt;outcome&gt;: &lt;criterion&gt;", split at the first ": ".</summary>
internal static class ChoiceList
{
    /// <summary>The longest criterion.</summary>
    public const int MaxCriterion = 400;

    /// <summary>Parses a literal list value.</summary>
    /// <param name="list">The literal; a value that isn't an array gives no entries and no faults.</param>
    /// <returns>The entries and faults.</returns>
    public static ChoiceParse Parse(JsonElement list)
    {
        if (list.ValueKind != JsonValueKind.Array)
        {
            return new ChoiceParse([], []);
        }

        var entries = new List<Choice>();
        var faults = new List<ChoiceFault>();
        var index = 0;
        foreach (var entry in list.EnumerateArray())
        {
            if (Entry(entry, index) is { } fault)
            {
                faults.Add(fault);
            }
            else
            {
                var text = entry.GetString()!; // why: Entry returns no fault only for a string
                var at = text.IndexOf(": ", StringComparison.Ordinal);
                entries.Add(new Choice(OutcomeName.From(text[..at]), text[(at + 2)..]));
            }

            index++;
        }

        return new ChoiceParse(entries, faults);
    }

    /// <summary>Whether <paramref name="name"/> is a choice outcome: [a-z][A-Za-z0-9]{0,31}, which OutcomeName accepts.</summary>
    private static bool IsOutcome(string name) =>
        name.Length is >= 1 and <= 32 && name[0] is >= 'a' and <= 'z' && name.All(static c => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9'));

    private static ChoiceFault? Entry(JsonElement entry, int index)
    {
        if (entry.ValueKind == JsonValueKind.Object)
        {
            return new ChoiceFault(index, ChoiceFaultKind.Mapping, string.Empty);
        }

        if (entry.ValueKind != JsonValueKind.String)
        {
            var kind = entry.ValueKind == JsonValueKind.Number ? "a number"
                : entry.ValueKind is JsonValueKind.True or JsonValueKind.False ? "a flag"
                : entry.ValueKind == JsonValueKind.Array ? "a list"
                : "null";
            return new ChoiceFault(index, ChoiceFaultKind.NotText, kind);
        }

        var text = entry.GetString()!; // why: a string element has text
        var at = text.IndexOf(": ", StringComparison.Ordinal);
        if (at < 0)
        {
            return new ChoiceFault(index, ChoiceFaultKind.NoSeparator, string.Empty);
        }

        if (!IsOutcome(text[..at]))
        {
            return new ChoiceFault(index, ChoiceFaultKind.BadName, text[..at]);
        }

        var length = text.Length - at - 2;
        return length is >= 1 and <= MaxCriterion ? null : new ChoiceFault(index, ChoiceFaultKind.BadLength, length.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
