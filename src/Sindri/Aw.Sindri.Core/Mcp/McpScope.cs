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

        bool registered;
        string? malformed;
        try
        {
            (registered, malformed) = read.File == ProviderFile.Codex ? (Codex(text), null) : Json(text);
        }
        catch (RegexMatchTimeoutException)
        {
            (registered, malformed) = (false, "too slow to check");
        }

#pragma warning disable CA1508 // flow analysis loses that the tuple assignment above can leave malformed null
        return malformed is not null
            ? new McpScopeFinding(read.File, read.Path, McpScopeState.Unreadable, malformed)
            : registered ? new McpScopeFinding(read.File, read.Path, McpScopeState.UserWide, Problem: null) : null;
#pragma warning restore CA1508
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

    /// <summary>
    /// Codex's config.toml: any TOML spelling of a 'sindri' server under mcp_servers — a [mcp_servers.sindri] table or a
    /// sub-table, a 'sindri' key in [mcp_servers], a top-level dotted key 'mcp_servers.sindri…', or an inline table
    /// 'mcp_servers = { sindri = … }'. Names may be bare or quoted. Read line by line (no TOML package): comments are cut,
    /// and a line that isn't a table header or a key is skipped.
    /// </summary>
    private static bool Codex(string text)
    {
        var table = string.Empty;
        foreach (var raw in text.Split('\n'))
        {
            var line = Uncommented(raw).Trim();
            if (line.StartsWith('['))
            {
                table = Key(line.Trim('[', ']'));
                if (IsSindri(table))
                {
                    return true;
                }

                continue;
            }

            var equals = line.IndexOf('=', StringComparison.Ordinal);
            if (equals <= 0)
            {
                continue;
            }

            var key = Key(line[..equals]);
            var full = table.Length == 0 ? key : table + "." + key;
            if (IsSindri(full) || (string.Equals(full, Servers, StringComparison.Ordinal) && InlineSindri.IsMatch(line[(equals + 1)..])))
            {
                return true;
            }
        }

        return false;
    }

    private const string Servers = "mcp_servers";

    private static bool IsSindri(string key) =>
        string.Equals(key, Servers + "." + ServerName, StringComparison.Ordinal) || key.StartsWith(Servers + "." + ServerName + ".", StringComparison.Ordinal);

    /// <summary>A dotted TOML key with each part trimmed and unquoted.</summary>
    private static string Key(string key) => string.Join('.', key.Split('.').Select(part => part.Trim().Trim('"', '\'')));

    /// <summary>The line up to a '#' that isn't inside a quoted string.</summary>
    private static string Uncommented(string line)
    {
        var quote = '\0';
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quote == '\0' && c == '#')
            {
                return line[..i];
            }

            quote = quote == '\0' && c is '"' or '\'' ? c : quote == c ? '\0' : quote;
        }

        return line;
    }

    /// <summary>A 'sindri' key inside an inline table: '{ sindri = …' or ', "sindri".url = …'.</summary>
    [GeneratedRegex("""(?:^|[{,])\s*(?:sindri|"sindri"|'sindri')\s*[=.]""", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex InlineSindri { get; }
}
