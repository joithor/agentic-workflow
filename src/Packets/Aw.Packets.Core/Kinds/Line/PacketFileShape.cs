using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Aw.Packets.Core.Kinds.Line;


/// <summary>What "well formed" means for a packet file of each <see cref="PacketFileFormat"/>; shared by every kind's checks.</summary>
internal static class PacketFileShape
{
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
        var strict = PacketReader.Strict(name, bytes.Span);
        if (strict.IsError)
        {
            return strict.Error.Message;
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
            var strict = PacketReader.Strict($"{name} line {number.ToString(CultureInfo.InvariantCulture)}", bytesOfLine);
            if (strict.IsError)
            {
                return strict.Error.Message;
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
}
