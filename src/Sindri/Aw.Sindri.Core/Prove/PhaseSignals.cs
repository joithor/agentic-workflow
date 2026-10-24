using System.Globalization;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Prove;

/// <summary>
/// The phase packet's signals.json as the line writes it (line spec §3.2; schemas/packets/design-packet.schema.json): a flat list <c language="text">{"signals": [{item, kind, statement, runs}]}</c>
/// where 'runs' is 'head' or 'base+head'. The prove station reads a signal as <c language="text">{id, kind, statement, runsAt}</c> (the proof pack's items carry them): <see cref="Of"/> groups the
/// list by item and gives each signal the id <c language="text">&lt;item&gt;-s&lt;n&gt;</c> (n counts from 1 in the item's own order) unless it carries one, and 'runs' becomes 'runsAt'. A file of another
/// shape yields no signals here; <see cref="Check"/> names why, so a Step can fail with the reason instead of reading nothing.
/// </summary>
public static class PhaseSignals
{
    /// <summary>The shape a signals.json must have, as a failure message quotes it.</summary>
    public static string Shape => "{\"signals\": [{\"item\": \"AC-1\", \"kind\": \"...\", \"statement\": \"...\", \"runs\": \"head\"}]}";

    /// <summary>Whether a signals.json has the shape the line writes.</summary>
    /// <param name="signals">The parsed file.</param>
    /// <returns>Null when it is the line's shape, else why it is not.</returns>
    public static string? Check(JsonNode? signals) => signals is JsonObject { } root && root["signals"] is JsonArray list && list.All(static s => s is JsonObject o && o["item"] is JsonValue)
        ? null
        : $"signals.json must be an object with a 'signals' list of signals that each name their 'item', like {Shape}";

    /// <summary>One item's signals, in the file's order.</summary>
    /// <param name="signals">The parsed signals.json; null or another shape yields none.</param>
    /// <param name="item">The item id.</param>
    /// <returns>Copies of the item's signals, each with an 'id' and a 'runsAt'.</returns>
    public static IReadOnlyList<JsonObject> Of(JsonNode? signals, string item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var found = new List<JsonObject>();
        if (Check(signals) is not null)
        {
            return found;
        }

        foreach (var own in ((JsonArray)signals!["signals"]!).OfType<JsonObject>().Where(s => string.Equals(Text(s["item"]), item, StringComparison.Ordinal))) // why: Check proved the shape
        {
            var signal = (JsonObject)own.DeepClone();
            signal["id"] ??= item + "-s" + (found.Count + 1).ToString(CultureInfo.InvariantCulture);
            if (signal["runsAt"] is null && signal["runs"] is { } runs)
            {
                _ = signal.Remove("runs"); // why: a node with a parent can't be added to another object
                signal["runsAt"] = runs;
            }

            found.Add(signal);
        }

        return found;
    }

    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
