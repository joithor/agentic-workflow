using System.Text.Json;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>Reads the 'with:' arguments the context Step inputs share. What it can't use it says in notes instead of dropping it silently.</summary>
internal static class ContextInputArguments
{
    /// <summary>The anchors: a list of strings, one string, or an object holding either under 'anchors'; empty when absent or of another shape.</summary>
    /// <param name="context">The input's context.</param>
    /// <param name="notes">Receives a note for anything that was ignored.</param>
    /// <returns>The anchors, in order, duplicates kept (the engine counts them once).</returns>
    internal static IReadOnlyList<string> Anchors(StepInputContext context, List<string> notes)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(notes);
        if (!context.Arguments.TryGetValue("anchors", out var value))
        {
            return [];
        }

        if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("anchors", out var inner))   // a resolved fact:anchors may be { "anchors": [...] }
        {
            value = inner;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString() is { Length: > 0 } one ? [one] : [];
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            notes.Add("anchors is not a list of text, so it was ignored");
            return [];
        }

        var items = value.EnumerateArray().ToList();
        var texts = items.Where(static e => e.ValueKind == JsonValueKind.String).Select(static e => e.GetString() ?? string.Empty).Where(static s => s.Length > 0).ToList();
        if (texts.Count < items.Count)
        {
            notes.Add(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{items.Count - texts.Count} of {items.Count} anchors are not text and were ignored"));
        }

        return texts;
    }
}
