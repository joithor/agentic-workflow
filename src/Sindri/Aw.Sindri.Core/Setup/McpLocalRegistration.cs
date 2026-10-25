using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Setup;

/// <summary>Whether Claude Code's local-scope entry for Sindri exists for a checkout (the per-repo registration `sindri mcp register` writes; a user-wide entry never counts).</summary>
public static class McpLocalRegistration
{
    private const string Server = "sindri";
    private const string RepoHeader = "X-Sindri-Repo";

    private static readonly JsonDocumentOptions _lenient = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    /// <summary>Reads Claude Code's .claude.json.</summary>
    /// <param name="claudeJson">The file's text, or null when it doesn't exist.</param>
    /// <param name="checkout">The repo's main checkout (Claude Code keys local scope by directory).</param>
    /// <param name="repo">The repo's name, which the entry's X-Sindri-Repo header must carry.</param>
    /// <returns>True when projects[checkout].mcpServers.sindri exists and names this repo; a file that can't be read counts as no entry.</returns>
    public static bool IsRegistered(string? claudeJson, string checkout, string repo)
    {
        ArgumentNullException.ThrowIfNull(checkout);
        ArgumentNullException.ThrowIfNull(repo);
        try
        {
            return claudeJson is not null && JsonNode.Parse(claudeJson, nodeOptions: null, _lenient) is JsonObject root
                && root["projects"] is JsonObject projects && projects[checkout] is JsonObject project
                && project["mcpServers"] is JsonObject servers && servers[Server] is JsonObject entry
                && entry["headers"] is JsonObject headers && string.Equals((string?)headers[RepoHeader], repo, StringComparison.Ordinal);
        }
        catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException)
        {
            return false;   // a damaged or oddly shaped file is no entry: registering says what claude thinks of it
        }
    }

    /// <summary>Reads a repo checkout's .cursor/mcp.json.</summary>
    /// <param name="mcpJson">The file's text, or null when it doesn't exist.</param>
    /// <param name="repo">The repo's name, which the entry's X-Sindri-Repo header must carry.</param>
    /// <returns>True when mcpServers.sindri exists and names this repo.</returns>
    public static bool IsRegisteredCursor(string? mcpJson, string repo)
    {
        ArgumentNullException.ThrowIfNull(repo);
        try
        {
            return mcpJson is not null && JsonNode.Parse(mcpJson, nodeOptions: null, _lenient) is JsonObject root
                && root["mcpServers"] is JsonObject servers && servers[Server] is JsonObject entry
                && entry["headers"] is JsonObject headers && string.Equals((string?)headers[RepoHeader], repo, StringComparison.Ordinal);
        }
        catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>Reads a repo checkout's .codex/config.toml.</summary>
    /// <param name="configToml">The file's text, or null when it doesn't exist.</param>
    /// <param name="repo">The repo's name, which the table's X-Sindri-Repo header must carry.</param>
    /// <returns>True when the [mcp_servers.sindri] table sets the header to this repo.</returns>
    public static bool IsRegisteredCodex(string? configToml, string repo)
    {
        ArgumentNullException.ThrowIfNull(repo);
        var inTable = false;
        foreach (var raw in (configToml ?? string.Empty).Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith('['))
            {
                inTable = line.StartsWith("[mcp_servers.sindri]", StringComparison.Ordinal);
            }
            else if (inTable && line.Contains(RepoHeader, StringComparison.Ordinal) && line.Contains("\"" + repo + "\"", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
