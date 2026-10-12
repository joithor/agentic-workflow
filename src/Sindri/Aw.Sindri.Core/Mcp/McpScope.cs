using System.Text.Json;
using System.Text.RegularExpressions;

namespace Aw.Sindri.Core.Mcp;

/// <summary>
/// Whether a provider file registers Sindri user-wide (context spec §3, API spec §8): registration is per work repo
/// (Claude Code's local scope) and never user-wide, so personal-repo sessions never see work context. Only the entry's
/// name is looked at; nothing else in the file is read into a finding.
/// </summary>
public static partial class McpScope
{
    /// <summary>The MCP server name Sindri is registered under.</summary>
    public const string ServerName = "sindri";

    /// <summary>What <paramref name="read"/> says, or null when it holds no user-wide Sindri entry (or doesn't exist).</summary>
    /// <param name="read">The file as read.</param>
    /// <returns>The finding, or null.</returns>
    public static McpScopeFinding? Judge(ProviderFileRead read)
    {
        ArgumentNullException.ThrowIfNull(read);
        if (read.Problem is { } problem)
        {
            return new McpScopeFinding(read.File, read.Path, McpScopeState.Unreadable, problem);
        }

        if (read.Text is not { } text)
        {
            return null;
        }

        var (registered, malformed) = read.File == ProviderFile.Codex ? (CodexTable.IsMatch(text), null) : Json(text);
        return malformed is not null
            ? new McpScopeFinding(read.File, read.Path, McpScopeState.Unreadable, malformed)
            : registered ? new McpScopeFinding(read.File, read.Path, McpScopeState.UserWide, Problem: null) : null;
    }

    /// <summary>Claude Code's and Cursor's files: a top-level mcpServers object with a 'sindri' member (Claude Code's local scope sits under projects, which is fine).</summary>
    private static (bool Registered, string? Malformed) Json(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            var root = document.RootElement;
            return (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("mcpServers", out var servers)
                && servers.ValueKind == JsonValueKind.Object && servers.TryGetProperty(ServerName, out _), null);
        }
        catch (JsonException)
        {
            return (false, "not valid JSON");
        }
    }

    /// <summary>Codex's config.toml: a [mcp_servers.sindri] table (or one of its sub-tables), the name bare or quoted.</summary>
    [GeneratedRegex("""^\s*\[\s*mcp_servers\s*\.\s*(?:sindri|"sindri"|'sindri')\s*(?:\.[^\]]*)?\]""", RegexOptions.Multiline | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CodexTable { get; }
}
