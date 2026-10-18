using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Aw.Sindri.Features.RegisterMcp;

/// <summary>The pure half of the registration: given a provider's current file text, what the file should say.</summary>
internal static partial class ProviderPlans
{
    private const string Server = "sindri";
    private const string RepoHeader = "X-Sindri-Repo";
    private const string CodexTable = "mcp_servers.sindri";

    private static readonly JsonDocumentOptions _lenient = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    /// <summary>The state of Claude Code's local-scope entry.</summary>
    internal enum ClaudeState { Absent, Same, Different }

    /// <summary>A file's planned text: <paramref name="Text"/> null with <paramref name="Problem"/> null is unchanged; a problem means the file can't be edited safely.</summary>
    /// <param name="Text">The file's new text, or null when it stays as it is.</param>
    /// <param name="Problem">Why the file can't be edited safely, or null.</param>
    internal sealed record FilePlan(string? Text, string? Problem);

    /// <summary>Claude Code's entry for <paramref name="checkout"/> in its .claude.json (a file it can't read counts as absent: the add will say why).</summary>
    internal static ClaudeState Claude(string? claudeJson, string checkout, string url, string repo)
    {
        if (claudeJson is null || Parse(claudeJson) is not { } root
            || root["projects"] is not JsonObject projects || projects[checkout] is not JsonObject project
            || project["mcpServers"] is not JsonObject servers || servers[Server] is not JsonObject entry)
        {
            return ClaudeState.Absent;
        }

        return Matches(entry, url, repo) ? ClaudeState.Same : ClaudeState.Different;
    }

    /// <summary>Cursor's project .cursor/mcp.json with the sindri entry merged in; other entries are untouched.</summary>
    internal static FilePlan Cursor(string? existing, string url, string repo)
    {
        JsonObject root;
        if (string.IsNullOrWhiteSpace(existing))
        {
            root = [];
        }
        else if (Parse(existing) is JsonObject parsed)
        {
            root = parsed;
        }
        else
        {
            return new FilePlan(Text: null, Problem: "isn't a JSON object");
        }

        if (root["mcpServers"] is not null and not JsonObject)
        {
            return new FilePlan(Text: null, Problem: "its mcpServers isn't an object");
        }

        var servers = root["mcpServers"] as JsonObject ?? [];
        if (servers[Server] is JsonObject current && Matches(current, url, repo))
        {
            return new FilePlan(Text: null, Problem: null);
        }

        servers[Server] = new JsonObject { ["url"] = url, ["headers"] = new JsonObject { [RepoHeader] = repo } };
        root["mcpServers"] = servers;
        return new FilePlan(Write(root), Problem: null);
    }

    /// <summary>Codex's project .codex/config.toml with the sindri table written; only that table is touched.</summary>
    internal static FilePlan Codex(string? existing, string url, string repo)
    {
        string[] canonical =
        [
            $"[{CodexTable}]",
            $"url = {Quoted(url)}",
            $"http_headers = {{ {Quoted(RepoHeader)} = {Quoted(repo)} }}",
        ];
        var scan = ScanCodex(TomlLines(existing));
        if (scan.Inline)
        {
            return new FilePlan(Text: null, Problem: "defines mcp_servers inline, which sindri doesn't edit");
        }

        return scan.Found.SequenceEqual(canonical, StringComparer.Ordinal)
            ? new FilePlan(Text: null, Problem: null)
            : new FilePlan(WithCodexTable(scan, canonical), Problem: null);
    }

    /// <summary>The trust level Codex's user config records for <paramref name="checkout"/> (a project .codex/config.toml loads only when it is "trusted"); null when none.</summary>
    internal static string? CodexTrust(string? userConfig, string checkout)
    {
        var inProject = false;
        foreach (var raw in (userConfig ?? string.Empty).Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith('['))
            {
                inProject = IsProject(line, checkout);
            }
            else if (inProject && TrustLevel.Match(line) is { Success: true } level)
            {
                return level.Groups["level"].Value;
            }
        }

        return null;
    }

    /// <summary>Codex's user config with <paramref name="checkout"/> marked trusted: a trust_level line added to its existing table, else a new table at the end. Nothing else changes.</summary>
    internal static string CodexTrustRow(string? userConfig, string checkout)
    {
        var text = userConfig ?? string.Empty;
        var lines = text.Split('\n').ToList();
        var header = lines.FindIndex(l => l.TrimStart().StartsWith('[') && IsProject(l.Trim(), checkout));
        if (header >= 0)
        {
            lines.Insert(header + 1, "trust_level = \"trusted\"");
            return string.Join('\n', lines);
        }

        var separator = text.Length == 0 ? string.Empty : text.EndsWith('\n') ? "\n" : "\n\n";
        return text + separator + $"[projects.{Quoted(checkout)}]\ntrust_level = \"trusted\"\n";
    }

    /// <summary>The file's lines with Windows line ends folded and the final newline's empty line dropped.</summary>
    private static List<string> TomlLines(string? existing)
    {
        var lines = (existing ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
        if (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines;
    }

    /// <summary>Splits the file into the lines outside the sindri tables and the sindri tables' settings; stops at an inline mcp_servers.</summary>
    private static CodexScan ScanCodex(IEnumerable<string> lines)
    {
        var scan = new CodexScan();
        var table = string.Empty;
        var sindri = false;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith('['))
            {
                table = TableKey(line);
                sindri = IsSindriTable(table);
                scan.MarkTable(sindri);
            }
            else if (!sindri && IsInlineServers(line, table))
            {
                scan.Inline = true;
                return scan;
            }

            scan.Take(raw, line, sindri);
        }

        return scan;
    }

    /// <summary>The kept lines with the canonical table where the first sindri table was, or appended after a blank line.</summary>
    private static string WithCodexTable(CodexScan scan, string[] canonical)
    {
        var output = scan.Kept;
        if (scan.First >= 0)
        {
            output.InsertRange(scan.First, [.. canonical, string.Empty]);
        }
        else
        {
            if (output.Count > 0 && output[^1].Trim().Length > 0)
            {
                output.Add(string.Empty);
            }

            output.AddRange(canonical);
        }

        return string.Join('\n', output) + "\n";
    }

    private static bool IsSindriTable(string table) =>
        table == CodexTable || table.StartsWith(CodexTable + ".", StringComparison.Ordinal);

    private static bool IsInlineServers(string line, string table) =>
        (table.Length == 0 && InlineServers.IsMatch(line)) || (table == "mcp_servers" && line.StartsWith(Server, StringComparison.Ordinal));

    private static bool IsProject(string header, string checkout)
    {
        var match = ProjectHeader.Match(header);
        return match.Success && string.Equals(match.Groups["double"].Success ? match.Groups["double"].Value : match.Groups["single"].Value, checkout, StringComparison.Ordinal);
    }

    private static bool Matches(JsonObject entry, string url, string repo) =>
        string.Equals((string?)entry["url"], url, StringComparison.Ordinal)
        && entry["headers"] is JsonObject headers && string.Equals((string?)headers[RepoHeader], repo, StringComparison.Ordinal);

    private static JsonNode? Parse(string text)
    {
        try
        {
            return JsonNode.Parse(text, nodeOptions: null, _lenient);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Write(JsonNode node)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            node.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    private static string Quoted(string value) => "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private static string TableKey(string header) =>
        string.Join('.', header.Trim('[', ']').Split('.').Select(part => part.Trim().Trim('"', '\'')));

    [GeneratedRegex("""^mcp_servers\s*=""", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex InlineServers { get; }

    [GeneratedRegex("""^\[projects\.(?:"(?<double>(?:[^"\\]|\\.)*)"|'(?<single>[^']*)')\]\s*(?:#.*)?$""", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ProjectHeader { get; }

    [GeneratedRegex("""^trust_level\s*=\s*["'](?<level>[^"']*)["']\s*(?:#.*)?$""", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TrustLevel { get; }

    /// <summary>What a pass over a Codex config found: the lines kept, the sindri tables' settings, where the first sindri table was, and whether mcp_servers is inline.</summary>
    private sealed class CodexScan
    {
        public List<string> Kept { get; } = [];

        public List<string> Found { get; } = [];

        public int First { get; private set; } = -1;

        public bool Inline { get; set; }

        public void MarkTable(bool sindri)
        {
            if (sindri && First < 0)
            {
                First = Kept.Count;
            }
        }

        public void Take(string raw, string line, bool sindri)
        {
            if (!sindri)
            {
                Kept.Add(raw);
            }
            else if (line.Length > 0 && !line.StartsWith('#'))
            {
                Found.Add(line);
            }
        }
    }
}
