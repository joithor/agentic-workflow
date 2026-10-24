using System.Buffers;
using System.Text;
using System.Text.Json;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Registry;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>The registry's column forms: 'via', the base pair, and the pins as JSON objects of id to hash (a block's also to {hash, interface}), in the pins' order.</summary>
internal static class RecipeRows
{
    internal static string Via(ApprovalVia via) => via switch
    {
        ApprovalVia.Desk => "desk",
        ApprovalVia.Freeze => "freeze",
        _ => throw new ArgumentOutOfRangeException(nameof(via), via, "Not an approval route"),
    };

    internal static ApprovalVia Via(string text) => text switch
    {
        "desk" => ApprovalVia.Desk,
        "freeze" => ApprovalVia.Freeze,
        _ => throw new InvalidDataException($"recipe_approvals.via holds '{text}', not 'desk' or 'freeze'."),
    };

    /// <summary>The base at <paramref name="ordinal"/> (id) and the next column (hash), or null when there is none.</summary>
    internal static RecipeRef? Base(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : new RecipeRef(RecipeId.From(reader.GetString(ordinal)), RecipeHash.From(reader.GetString(ordinal + 1)));

    /// <summary>A pin is its hash, or {"hash","interface"} once the interface is pinned too; a row from before reads as the plain hash.</summary>
    internal static string Blocks(IReadOnlyList<BlockPin> pins)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var pin in pins)
            {
                if (pin.Interface is { } shape)
                {
                    writer.WriteStartObject(pin.Block.Value);
                    writer.WriteString("hash", pin.Hash.Value);
                    writer.WriteString("interface", shape.Value);
                    writer.WriteEndObject();
                }
                else
                {
                    writer.WriteString(pin.Block.Value, pin.Hash.Value);
                }
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    internal static string Children(IReadOnlyList<ChildPin> pins) => Object(pins.Select(static p => (p.Recipe.Value, p.Hash.Value)));

    internal static IReadOnlyList<BlockPin> BlockPins(string json)
    {
        using var document = JsonDocument.Parse(json);
        return [.. document.RootElement.EnumerateObject().Select(static p => p.Value.ValueKind == JsonValueKind.Object
            ? new BlockPin(BlockId.From(p.Name), DescriptorHash.From(Text(p, "hash")), DescriptorHash.From(Text(p, "interface")))
            : new BlockPin(BlockId.From(p.Name), DescriptorHash.From(p.Value.GetString() ?? throw new InvalidDataException($"Pin '{p.Name}' holds no hash."))))];
    }

    internal static IReadOnlyList<ChildPin> ChildPins(string json) =>
        [.. Members(json).Select(static m => new ChildPin(RecipeId.From(m.Key), RecipeHash.From(m.Value)))];

    private static string Object(IEnumerable<(string Key, string Value)> members)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var (key, value) in members)
            {
                writer.WriteString(key, value);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static string Text(JsonProperty pin, string name) =>
        pin.Value.TryGetProperty(name, out var value) && value.GetString() is { } text ? text : throw new InvalidDataException($"Pin '{pin.Name}' holds no {name}.");

    private static List<KeyValuePair<string, string>> Members(string json)
    {
        using var document = JsonDocument.Parse(json);
        return [.. document.RootElement.EnumerateObject().Select(static p => KeyValuePair.Create(p.Name, p.Value.GetString()
            ?? throw new InvalidDataException($"Pin '{p.Name}' holds no hash.")))];
    }
}
