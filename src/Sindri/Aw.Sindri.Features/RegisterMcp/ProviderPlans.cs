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

    private static readonly JsonDocumentOptions Lenient = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    /// <summary>The state of Claude Code's local-scope entry.</summary>
    internal enum ClaudeState { Absent, Same, Different }

    /// <summary>A file's planned text: <c>Text</c> null with <c>Problem</c> null is unchanged; a problem means the file can't be edited safely.</summary>
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
            return new FilePlan(null, "isn't a JSON object");
        }

        if (root["mcpServers"] is not null and not JsonObject)
        {
            return new FilePlan(null, "its mcpServers isn't an object");
        }

        var servers = root["mcpServers"] as JsonObject ?? [];
        if (servers[Server] is JsonObject current && Matches(current, url, repo))
        {
            return new FilePlan(null, null);
        }

        servers[Server] = new JsonObject { ["url"] = url, ["headers"] = new JsonObject { [RepoHeader] = repo } };
        root["mcpServers"] = servers;
        return new FilePlan(Write(root), null);
    }

    /// <summary>Codex's project .codex/config.toml with the sindri table written; only that table is touched.</summary>
    internal static FilePlan Codex(string? existing, string url, string repo)
    {
        var canonical = new[]
        {
            $"[{CodexTable}]",
            $"url = {Quoted(url)}",
            $"http_headers = {{ {Quoted(RepoHeader)} = {Quoted(repo)} }}",
        };
        var lines = (existing ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
        if (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        var output = new List<string>();
        var current = new List<string>();
        var table = string.Empty;
        var sindri = false;
        var first = -1;
        var found = new List<string>();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith('['))
            {
                table = TableKey(line);
                sindri = table == CodexTable || table.StartsWith(CodexTable + ".", StringComparison.Ordinal);
                if (sindri && first < 0)
                {
                    first = output.Count;
                }
            }
            else if (!sindri && (table.Length == 0 && InlineServers.IsMatch(line) || table == "mcp_servers" && line.StartsWith(Server, StringComparison.Ordinal)))
            {
                return new FilePlan(null, "defines mcp_servers inline, which sindri doesn't edit");
            }

            if (sindri)
            {
                if (line.Length > 0 && !line.StartsWith('#'))
                {
                    found.Add(line);
                }
            }
            else
            {
                output.Add(raw);
            }
        }

        current.AddRange(found);
        if (current.SequenceEqual(canonical, StringComparer.Ordinal))
        {
            return new FilePlan(null, null);
        }

        if (first < 0)
        {
            if (output.Count > 0 && output[^1].Trim().Length > 0)
            {
                output.Add(string.Empty);
            }

            output.AddRange(canonical);
        }
        else
        {
            output.InsertRange(first, [.. canonical, string.Empty]);
        }

        return new FilePlan(string.Join('\n', output) + "\n", null);
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
                return level.Groups[1].Value;
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

    private static bool IsProject(string header, string checkout)
    {
        var match = ProjectHeader.Match(header);
        return match.Success && string.Equals(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value, checkout, StringComparison.Ordinal);
    }

    private static bool Matches(JsonObject entry, string url, string repo) =>
        string.Equals((string?)entry["url"], url, StringComparison.Ordinal)
        && entry["headers"] is JsonObject headers && string.Equals((string?)headers[RepoHeader], repo, StringComparison.Ordinal);

    private static JsonNode? Parse(string text)
    {
        try
        {
            return JsonNode.Parse(text, nodeOptions: null, Lenient);
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

    [GeneratedRegex("""^\[projects\.(?:"((?:[^"\\]|\\.)*)"|'([^']*)')\]\s*(?:#.*)?$""", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ProjectHeader { get; }

    [GeneratedRegex("""^trust_level\s*=\s*["']([^"']*)["']\s*(?:#.*)?$""", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TrustLevel { get; }
}
