namespace Aw.Packets.Core.Kinds;

/// <summary>The packet-kind registry: every kind aw-packets knows, by name.</summary>
public static class PacketKinds
{
    /// <summary>The build packet (addendum §17.4): <c>packet.json</c> and the files it links.</summary>
    public static PacketKind Build { get; } = new("build", []);

    /// <summary>
    /// The scope packet of the scoping station (scoping spec §8.3): twelve fixed files. <c>lenses.json</c> has a typed
    /// schema; the others are checked for their shape only, and Sindri's <c>ScopePacketChecks</c> owns what they mean.
    /// </summary>
    public static PacketKind Scoping { get; } = new("scoping",
    [
        new("scope-map.json", PacketFileFormat.Json),
        new("acceptance.json", PacketFileFormat.Json),
        new("coverage.json", PacketFileFormat.Json),
        new("lenses.json", PacketFileFormat.Json),
        new("size.json", PacketFileFormat.Json),
        new("questions.json", PacketFileFormat.Json),
        new("answers.jsonl", PacketFileFormat.JsonLines),
        new("challenge.json", PacketFileFormat.Json),
        new("rounds.jsonl", PacketFileFormat.JsonLines),
        new("runs.json", PacketFileFormat.Json),
        new("sources.json", PacketFileFormat.Json),
        new("brief.md", PacketFileFormat.Text),
    ]);

    /// <summary>Every kind, in the order help lists them.</summary>
    public static IReadOnlyList<PacketKind> All { get; } = [Build, Scoping];

    /// <summary>Finds a kind by its name (ordinal, lower-case).</summary>
    /// <param name="name">The name given to <c>--kind</c>.</param>
    /// <returns>The kind, or null when none has that name.</returns>
    public static PacketKind? Find(string name) => All.FirstOrDefault(k => string.Equals(k.Name, name, StringComparison.Ordinal));
}
