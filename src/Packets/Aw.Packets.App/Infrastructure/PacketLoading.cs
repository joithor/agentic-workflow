using System.Globalization;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Packets.Core;
using Aw.Packets.Core.Model;

namespace Aw.Packets.App.Infrastructure;

/// <summary>
/// Loads a packet through a file reader, so the disk adapter and the in-memory fake share one parser: packet.json
/// first, then every file it links (relative, inside the packet directory), each JSON file at schemaVersion 1.
/// </summary>
internal static class PacketLoading
{
    internal const string ManifestFile = "packet.json";
    internal const int SchemaVersion = 1;

    /// <summary>Reads one file by its path relative to the packet directory: the bytes, null when it is missing,
    /// or AW-PACKETS-008 when it exists but can't be read.</summary>
    internal delegate Result<ReadOnlyMemory<byte>?> FileReader(string relativePath);

    internal static Result<LoadedPacket> Load(FileReader read, JsonSerializerOptions json)
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

    internal static bool IsSafeRelative(string path) =>
        path.Length > 0
        && !path.Contains('\\', StringComparison.Ordinal)
        && !path.StartsWith('/')
        && !path.Contains(':', StringComparison.Ordinal)
        && path.Split('/').All(static s => s.Length > 0 && s is not ("." or ".."));

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

        return new LoadedPacket(new Packet(manifest, decisions.Value, outOfScope.Value, tasks.Value), files);
    }

    private static IEnumerable<string> Links(PacketLinks links)
    {
        IEnumerable<string?> all = [links.Decisions, links.OutOfScope, links.Tasks, .. links.Design, .. links.Plans, links.Workflow];
        return all.OfType<string>().Distinct(StringComparer.Ordinal);
    }

    private static Result<ReadOnlyMemory<byte>> ReadLink(FileReader read, string link)
    {
        if (!IsSafeRelative(link) || string.Equals(link, ManifestFile, StringComparison.Ordinal))
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
}
