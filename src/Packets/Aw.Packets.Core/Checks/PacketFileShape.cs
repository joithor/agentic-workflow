using System.Globalization;
using System.Text;
using System.Text.Json;
using Aw.Packets.Core.Kinds;

namespace Aw.Packets.Core.Checks;


/// <summary>What "well formed" means for a packet file of each <see cref="PacketFileFormat"/>; shared by every kind's checks.</summary>
internal static class PacketFileShape
{
    private static readonly JsonDocumentOptions _strict = new() { AllowDuplicateProperties = false };

    /// <summary>Why a file's bytes aren't well formed for its format, or null when they are.</summary>
    /// <param name="spec">The file's name and format.</param>
    /// <param name="bytes">Its bytes.</param>
    /// <returns>The problem, or null.</returns>
    internal static string? Problem(PacketFileSpec spec, ReadOnlyMemory<byte> bytes) => spec.Format switch
    {
        PacketFileFormat.Json => JsonProblem(spec.Name, bytes),
        PacketFileFormat.JsonLines => LinesProblem(spec.Name, bytes),
        PacketFileFormat.Text or _ => TextProblem(spec.Name, bytes),
    };

    /// <summary>The fix text for a malformed file.</summary>
    /// <param name="spec">The file's name and format.</param>
    /// <returns>One sentence.</returns>
    internal static string Fix(PacketFileSpec spec) => spec.Format switch
    {
        PacketFileFormat.JsonLines => $"Write {spec.Name} as one JSON object per line.",
        PacketFileFormat.Text => $"Save {spec.Name} as UTF-8.",
        PacketFileFormat.Json or _ => $"Write {spec.Name} as one JSON object, UTF-8 without a byte order mark, with no duplicate keys and no null in a list.",
    };

    private static string? JsonProblem(string name, ReadOnlyMemory<byte> bytes)
    {
        if (StrictProblem(name, bytes.Span) is { } strictProblem)
        {
            return strictProblem;
        }

        using var document = JsonDocument.Parse(bytes);
        return document.RootElement.ValueKind == JsonValueKind.Object ? null : $"{name} is not a JSON object.";
    }

    private static string? LinesProblem(string name, ReadOnlyMemory<byte> bytes)
    {
        if (TextProblem(name, bytes) is { } text)
        {
            return text;
        }

        var number = 0;
        foreach (var line in Encoding.UTF8.GetString(bytes.Span).Split('\n'))
        {
            number++;
            if (line.AsSpan().Trim().IsEmpty)
            {
                continue;
            }

            var bytesOfLine = Encoding.UTF8.GetBytes(line);
            if (StrictProblem($"{name} line {number.ToString(CultureInfo.InvariantCulture)}", bytesOfLine) is { } strictProblem)
            {
                return strictProblem;
            }

            using var document = JsonDocument.Parse(bytesOfLine);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return $"{name} line {number.ToString(CultureInfo.InvariantCulture)} is not a JSON object.";
            }
        }

        return null;
    }

    private static string? TextProblem(string name, ReadOnlyMemory<byte> bytes)
    {
        try
        {
            _ = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes.Span);
            return null;
        }
        catch (DecoderFallbackException)
        {
            return $"{name} is not valid UTF-8.";
        }
    }

    internal static string? StrictProblem(string path, ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]))
        {
            return $"{path} starts with a UTF-8 byte order mark; save it as UTF-8 without one.";
        }

        try
        {
            using var document = JsonDocument.Parse(bytes.ToArray(), _strict);   // throws on a duplicate key at any depth
            return NullInList(document.RootElement, "$") is { } where
                ? $"{path}: {where} is null; lists hold values, never null."
                : null;
        }
        catch (JsonException ex)
        {
            return $"{path}: {ex.Message}";
        }
    }

    // The JSON path of the first null inside an array (in document order), or null. Iterative, so a deep document
    // can't overflow the stack: children are pushed in reverse so they pop in document order.
    private static string? NullInList(JsonElement root, string rootPath)
    {
        var pending = new Stack<(JsonElement Element, string Path)>();
        pending.Push((root, rootPath));
        while (pending.TryPop(out var next))
        {
            if (next.Element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in next.Element.EnumerateObject().Reverse())
                {
                    pending.Push((property.Value, $"{next.Path}.{property.Name}"));
                }
            }
            else if (next.Element.ValueKind == JsonValueKind.Array)
            {
                var items = next.Element.EnumerateArray().ToList();
                if (items.FindIndex(static i => i.ValueKind == JsonValueKind.Null) is var at and >= 0)
                {
                    return string.Create(CultureInfo.InvariantCulture, $"{next.Path}[{at}]");
                }

                for (var index = items.Count - 1; index >= 0; index--)
                {
                    pending.Push((items[index], string.Create(CultureInfo.InvariantCulture, $"{next.Path}[{index}]")));
                }
            }
        }

        return null;
    }
}