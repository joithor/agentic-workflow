using System.Globalization;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Packets.Core.Model;

namespace Aw.Packets.Core;

/// <summary>Parses a sizes file: strict JSON (as every packet file), schemaVersion 1, no negative limit. Pure: every ISizesSource adapter supplies the bytes.</summary>
public static class SizesReader
{
    /// <summary>Parses <paramref name="bytes"/>.</summary>
    /// <param name="name">The file's name, for messages.</param>
    /// <param name="bytes">The file.</param>
    /// <param name="json">The package's JSON options.</param>
    /// <returns>The sizes, or AW-PACKETS-009 saying what is wrong.</returns>
    public static Result<SizesFile> Parse(string name, ReadOnlyMemory<byte> bytes, JsonSerializerOptions json)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (Checks.PacketFileShape.StrictProblem(name, bytes.Span) is { } problem)
        {
            return PacketsErrors.SizesUnusable.With(problem);
        }

        var parsed = AwJson.Deserialize<SizesFile>(bytes.Span, json);
        if (parsed.IsError)
        {
            return PacketsErrors.SizesUnusable.With($"{name}: {parsed.Error.Message}");
        }

        var sizes = parsed.Value;
        if (sizes.SchemaVersion != PacketReader.SchemaVersion)
        {
            return PacketsErrors.SizesUnusable.With(string.Create(CultureInfo.InvariantCulture, $"{name}: schemaVersion {sizes.SchemaVersion}; this build reads {PacketReader.SchemaVersion}."));
        }

        return sizes.Sizes.All().Any(Negative)
            ? PacketsErrors.SizesUnusable.With($"{name}: a limit is negative; a limit is zero or more (leave a budget limit out to not check it).")
            : sizes;
    }

    private static bool Negative(SizeLimit limit) =>
        limit.Files < 0 || limit.Items < 0 || limit.Budget.Tokens < 0 || limit.Budget.Turns < 0 || limit.Budget.WallMinutes < 0;
}
