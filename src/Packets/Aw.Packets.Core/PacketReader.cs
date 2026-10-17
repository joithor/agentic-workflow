using System.Globalization;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Packets.Core.Model;

namespace Aw.Packets.Core;

/// <summary>
/// Reads a packet through a file reader: packet.json first, then every file it links (relative, inside the packet
/// directory), each JSON file at schemaVersion 1, with no duplicate keys and no byte order mark. Pure: every
/// IPacketSource adapter supplies the reader and gets the same parsing, link safety and errors.
/// </summary>
public static class PacketReader
{
    /// <summary>The manifest's file name.</summary>
    public const string ManifestFile = "packet.json";

    /// <summary>The most tasks a packet may hold; a bigger one is AW-PACKETS-003 (split it into several packets).</summary>
    public const int MaxTasks = 2000;

    /// <summary>The schemaVersion this build reads.</summary>
    public const int SchemaVersion = 1;

    private static readonly JsonDocumentOptions _strict = new() { AllowDuplicateProperties = false };

    /// <summary>Reads one file by its path relative to the packet directory.</summary>
    /// <param name="relativePath">The path, with '/' separators.</param>
    /// <returns>The bytes, null when the file is missing, or an error (AW-PACKETS-003 or -008) when it can't be read.</returns>
    public delegate Result<ReadOnlyMemory<byte>?> FileReader(string relativePath);

    /// <summary>Reads and parses a packet.</summary>
    /// <param name="read">Reads one file.</param>
    /// <param name="json">The packet file options (AwJson over the package's JSON context).</param>
    /// <returns>The packet, or AW-PACKETS-002, -003, -004 or the reader's error.</returns>
    public static Result<LoadedPacket> Load(FileReader read, JsonSerializerOptions json)
    {
        var manifestBytes = read(ManifestFile);
        if (manifestBytes.IsError)
        {
            return manifestBytes.Error;
        }

        if (manifestBytes.Value is not { } manifestRaw)
        {
            return PacketsErrors.NotFound.With($"No {ManifestFile} in the packet directory.");
        }

        var manifest = Parse<PacketManifest>(ManifestFile, manifestRaw, json, static m => m.SchemaVersion);
        return manifest.IsError ? manifest.Error : LoadLinked(read, json, manifest.Value, manifestRaw);
    }

    private static Result<LoadedPacket> LoadLinked(FileReader read, JsonSerializerOptions json, PacketManifest manifest, ReadOnlyMemory<byte> manifestRaw)
    {
        var files = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal) { [ManifestFile] = manifestRaw };
        foreach (var link in Links(manifest.Links))
        {
            var bytes = ReadLink(read, link);
            if (bytes.IsError)
            {
                return bytes.Error;
            }

            files[link] = bytes.Value;
        }

        var links = manifest.Links;
        var decisions = Parse<DecisionLog>(links.Decisions, files[links.Decisions], json, static d => d.SchemaVersion);
        var outOfScope = Parse<OutOfScope>(links.OutOfScope, files[links.OutOfScope], json, static o => o.SchemaVersion);
        var tasks = Parse<TaskPlan>(links.Tasks, files[links.Tasks], json, static t => t.SchemaVersion);
        if (decisions.IsError || outOfScope.IsError || tasks.IsError)
        {
            return decisions.IsError ? decisions.Error : outOfScope.IsError ? outOfScope.Error : tasks.Error;
        }

        if (tasks.Value.Tasks.Count > MaxTasks)
        {
            return PacketsErrors.Malformed.With(string.Create(
                CultureInfo.InvariantCulture,
                $"{links.Tasks} holds {tasks.Value.Tasks.Count} tasks; a packet holds at most {MaxTasks}: split it into several packets."));
        }

        return new LoadedPacket(new Packet(manifest, decisions.Value, outOfScope.Value, tasks.Value), files);
    }

    private static IEnumerable<string> Links(PacketLinks links)
    {
        IEnumerable<string?> all = [links.Decisions, links.OutOfScope, links.Tasks, .. links.Design, .. links.Plans, links.Workflow];
        return all.OfType<string>().Distinct(StringComparer.Ordinal);
    }

    private static Result<ReadOnlyMemory<byte>> ReadLink(FileReader read, string link)
    {
        if (!RelativePath.IsSafe(link) || string.Equals(link, ManifestFile, StringComparison.Ordinal))
        {
            return PacketsErrors.Malformed.With(
                $"packet.json links '{link}': links are relative paths with '/' inside the packet directory (no '..', no root), and never packet.json.");
        }

        var bytes = read(link);
        if (bytes.IsError)
        {
            return bytes.Error;
        }

        return bytes.Value is { } found ? found : PacketsErrors.Malformed.With($"packet.json links '{link}', which is missing.");
    }

    private static Result<T> Parse<T>(string path, ReadOnlyMemory<byte> bytes, JsonSerializerOptions json, Func<T, int> version)
    {
        var strict = Strict(path, bytes.Span);
        if (strict.IsError)
        {
            return strict.Error;
        }

        var parsed = AwJson.Deserialize<T>(bytes.Span, json);
        if (parsed.IsError)
        {
            return PacketsErrors.Malformed.With($"{path}: {parsed.Error.Message}");
        }

        var declared = version(parsed.Value);
        return declared == SchemaVersion
            ? parsed
            : PacketsErrors.UnsupportedSchema.With(string.Create(CultureInfo.InvariantCulture, $"{path}: schemaVersion {declared}; this build reads {SchemaVersion}."));
    }

    internal static Result<Unit> Strict(string path, ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]))
        {
            return PacketsErrors.Malformed.With($"{path} starts with a UTF-8 byte order mark; save it as UTF-8 without one.");
        }

        try
        {
            using var document = JsonDocument.Parse(bytes.ToArray(), _strict);   // throws on a duplicate key at any depth
            return NullInList(document.RootElement, "$") is { } where
                ? PacketsErrors.Malformed.With($"{path}: {where} is null; lists hold values, never null.")
                : Unit.Value;
        }
        catch (JsonException ex)
        {
            return PacketsErrors.Malformed.With($"{path}: {ex.Message}");
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
