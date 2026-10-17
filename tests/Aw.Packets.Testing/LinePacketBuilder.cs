using System.Text;
using Aw.Kernel.Json;
using Aw.Packets.Core.Kinds;

namespace Aw.Packets.Testing;

/// <summary>
/// Builds the files of a line packet (design, plan, phase, proof, review or handoff). The default passes every check
/// of its kind against <see cref="PacketBuilder.BaseFiles"/> at <see cref="PacketBuilder.BaseSha"/>.
/// </summary>
internal sealed class LinePacketBuilder
{
    /// <summary>A 'sha256:' hash used where a test needs one.</summary>
    public static readonly string Hash = "sha256:" + new string('a', 64);

    /// <summary>A second hash.</summary>
    public static readonly string OtherHash = "sha256:" + new string('b', 64);

    private const string Sha = PacketBuilder.BaseSha;

    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);
    private bool _indexSet;

    /// <summary>Creates the builder with the kind's gold files.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="id">The packet id (a phase's default is 'expiry-p1').</param>
    public LinePacketBuilder(PacketKind kind, string? id = null)
    {
        Kind = kind;
        var packetId = id ?? (kind.Name == "phase" ? "expiry-p1" : $"expiry-{kind.Name}");
        foreach (var spec in kind.Files)
        {
            _files[spec.Name] = Encoding.UTF8.GetBytes(spec.Format switch
            {
                PacketFileFormat.Json => "{ \"schemaVersion\": 1 }\n",
                PacketFileFormat.JsonLines => "{ \"round\": 1 }\n{ \"round\": 2 }\n",
                PacketFileFormat.Text or _ => "# Text\n",
            });
        }

        _files["packet.json"] = Encoding.UTF8.GetBytes(Manifest(kind, packetId));
        _files["index.json"] = Encoding.UTF8.GetBytes(Index(kind.Name, "[]", "null", "null"));
        foreach (var (name, text) in KindFiles(kind.Name))
        {
            _files[name] = Encoding.UTF8.GetBytes(text);
        }

        if (kind.Name == "plan")
        {
            _files["tasks.json"] = AwJson.Serialize(A.Packet().Build().Tasks, PacketFiles.Json);
        }

        if (kind.Name == "proof")
        {
            _files["app-run/run.txt"] = "ran\n"u8.ToArray();
            _files["evidence/shot.txt"] = "shot\n"u8.ToArray();
        }
    }

    /// <summary>The kind built.</summary>
    public PacketKind Kind { get; }

    /// <summary>Replaces (or adds) a file.</summary>
    /// <param name="name">The path relative to the packet directory.</param>
    /// <param name="text">Its text.</param>
    /// <returns>This builder.</returns>
    public LinePacketBuilder With(string name, string text) => With(name, Encoding.UTF8.GetBytes(text));

    /// <summary>Replaces (or adds) a file.</summary>
    /// <param name="name">The path relative to the packet directory.</param>
    /// <param name="bytes">Its bytes.</param>
    /// <returns>This builder.</returns>
    public LinePacketBuilder With(string name, byte[] bytes)
    {
        _files[name] = bytes;
        _indexSet |= name == "index.json";
        return this;
    }

    /// <summary>Replaces the first occurrence of some text inside a file.</summary>
    /// <param name="name">The file.</param>
    /// <param name="from">The text to replace (must occur).</param>
    /// <param name="to">The replacement.</param>
    /// <returns>This builder.</returns>
    public LinePacketBuilder Edit(string name, string from, string to)
    {
        Materialize();
        var current = Encoding.UTF8.GetString(_files[name]);
        var at = current.IndexOf(from, StringComparison.Ordinal);
        if (at < 0)
        {
            throw new InvalidOperationException($"'{from}' does not occur in {name}.");
        }

        return With(name, string.Concat(current.AsSpan(0, at), to, current.AsSpan(at + from.Length)));
    }

    /// <summary>Removes a file.</summary>
    /// <param name="name">The path.</param>
    /// <returns>This builder.</returns>
    public LinePacketBuilder Without(string name)
    {
        _ = _files.Remove(name);
        return this;
    }

    /// <summary>The files, by path: a fresh map a test may change.</summary>
    /// <returns>The files.</returns>
    public Dictionary<string, byte[]> Build()
    {
        Materialize();
        return new Dictionary<string, byte[]>(_files, StringComparer.Ordinal);
    }

    // index.json follows acceptance.json (PKT-xxx-006): self is its sha256, extends its 'parent'; until a test sets the index itself.
    private void Materialize()
    {
        if (_indexSet || !_files.ContainsKey("index.json") || !_files.TryGetValue("acceptance.json", out var acceptance))
        {
            return;
        }

        var parent = System.Text.RegularExpressions.Regex.Match(Encoding.UTF8.GetString(acceptance), "\"parent\": \"(?<hash>sha256:[0-9a-f]{64})\"", System.Text.RegularExpressions.RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1)).Groups["hash"].Value;
        _files["index.json"] = Encoding.UTF8.GetBytes(Index(Kind.Name, "[]", $"\"{FileHash(acceptance)}\"", $"\"{parent}\""));
    }

    /// <summary>The 'sha256:' hash of a file's bytes.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The hash.</returns>
    public static string FileHash(byte[] bytes) => "sha256:" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));

    /// <summary>An index.json.</summary>
    /// <param name="kind">The kind it says.</param>
    /// <param name="parents">The parents array, as JSON.</param>
    /// <param name="self">The own acceptance hash, as JSON (a quoted hash or null).</param>
    /// <param name="extends">The extended acceptance hash, as JSON.</param>
    /// <returns>The text.</returns>
    public static string Index(string kind, string parents, string self, string extends) =>
        $"{{ \"schemaVersion\": 1, \"kind\": \"{kind}\", \"parents\": {parents}, \"acceptance\": {{ \"self\": {self}, \"extends\": {extends} }} }}\n";

    /// <summary>An acceptance.json with one item.</summary>
    /// <param name="status">The item's status; a passing item gets one evidence ref.</param>
    /// <returns>The text.</returns>
    public static string Acceptance(string status)
    {
        var evidence = status == "passing"
            ? $"[{{ \"kind\": \"test\", \"path\": \"evidence/shot.txt\", \"sha256\": \"{Hash}\", \"commit\": \"{Sha}\", \"at\": \"2026-10-16T09:00:00Z\" }}]"
            : "[]";
        return $"{{ \"schemaVersion\": 1, \"parent\": \"{OtherHash}\", \"items\": [{{ \"id\": \"AC-1\", \"text\": \"The link expires.\", \"form\": \"event\", \"status\": \"{status}\", \"evidence\": {evidence}, \"parent\": null }}] }}\n";
    }

    /// <summary>The gold phase.json.</summary>
    public const string PhaseJson =
        "{\n" +
        "  \"schemaVersion\": 1,\n" +
        "  \"goal\": \"Add the expiry field\",\n" +
        "  \"kind\": \"feature\",\n" +
        "  \"deps\": [],\n" +
        "  \"size\": \"S\",\n" +
        "  \"base\": { \"rule\": \"default-branch\", \"ref\": \"main\" },\n" +
        "  \"allowedFiles\": [\"src/**\", \"new: src/expiry.cs\"],\n" +
        "  \"testChange\": [{ \"path\": \"tests/links.test.ts\", \"reason\": \"createLink gains an argument\" }],\n" +
        "  \"readSeeds\": [\"code:src/a.cs\"],\n" +
        "  \"budget\": { \"tokens\": 100000, \"turns\": 40, \"wallMinutes\": 30 }\n" +
        "}\n";

    private static Dictionary<string, string> KindFiles(string kind) => kind switch
    {
        "design" => new(StringComparer.Ordinal) { ["acceptance.json"] = Acceptance("failing") },
        "proof" => new(StringComparer.Ordinal) { ["acceptance.json"] = Acceptance("passing") },
        "phase" => new(StringComparer.Ordinal)
        {
            ["acceptance.json"] = Acceptance("failing"),
            ["phase.json"] = PhaseJson,
            ["holdout.json"] = $"{{ \"schemaVersion\": 1, \"setHash\": \"{Hash}\", \"count\": 3 }}\n",
        },
        "handoff" => new(StringComparer.Ordinal)
        {
            ["pr.json"] = $"{{ \"schemaVersion\": 1, \"base\": \"main\", \"headBranch\": \"sindri/expiry/p1\", \"headSha\": \"{Sha}\", \"stackParent\": null, \"title\": \"feat: expiry\", \"labels\": [\"area:sharing\"] }}\n",
        },
        _ => new(StringComparer.Ordinal),
    };

    private static string Manifest(PacketKind kind, string id)
    {
        var links = new StringBuilder();
        foreach (var spec in kind.Files.Where(static f => f.Link is not null))
        {
            _ = links.Append(links.Length == 0 ? "" : ", ").Append('"').Append(spec.Link).Append("\": \"").Append(spec.Name).Append('"');
        }

        foreach (var directory in kind.Directories)
        {
            _ = links.Append(", \"").Append(directory.Link).Append("\": [\"").Append(directory.Prefix).Append(directory.Name == "app-run" ? "run.txt" : "shot.txt").Append("\"]");
        }

        return $"{{ \"schemaVersion\": 1, \"kind\": \"{kind.Name}\", \"id\": \"{id}\", \"repo\": \"example/repo\", \"baseSha\": \"{Sha}\", \"status\": \"draft\", \"hash\": null, \"links\": {{ {links} }} }}\n";
    }
}
