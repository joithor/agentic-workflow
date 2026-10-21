using System.Text;
using System.Text.RegularExpressions;

namespace Aw.Sindri.Features.RegisterMcp;

/// <summary>The TOML half of the planner: one pass over a Codex config, and the header and string helpers it needs.</summary>
internal static partial class ProviderPlans
{
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

    /// <summary>One pass over a Codex config: the lines to keep, the sindri table's url and http_headers, or why it can't be edited.</summary>
    private static CodexScan ScanCodex(IEnumerable<string> lines)
    {
        var scan = new CodexScan();
        var table = string.Empty;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith('['))
            {
                table = TableKey(line);
            }

            if (IsUnsupported(line, table))
            {
                scan.Problem = Unsupported;
                return scan;
            }

            scan.Take(raw, line, table);
        }

        return scan;
    }

    /// <summary>The kept lines with url and http_headers right after the sindri table's header, or a new table appended after a blank line.</summary>
    private static string WithCodexSettings(CodexScan scan, string[] settings)
    {
        var output = scan.Kept;
        if (scan.Header >= 0)
        {
            output.InsertRange(scan.Header, settings);
        }
        else
        {
            if (output.Count > 0 && output[^1].Trim().Length > 0)
            {
                output.Add(string.Empty);
            }

            output.Add($"[{CodexTable}]");
            output.AddRange(settings);
        }

        return string.Join('\n', output) + "\n";
    }

    /// <summary>A form of the sindri server this planner can't edit without rewriting the owner's TOML: an inline or dotted mcp_servers or sindri key, or headers as a table or dotted keys.</summary>
    private static bool IsUnsupported(string line, string table) => table switch
    {
        "" => RootServers.IsMatch(line),
        "mcp_servers" => SindriKey.IsMatch(line),
        CodexTable => line.StartsWith("http_headers.", StringComparison.Ordinal),
        _ => string.Equals(table, CodexTable + ".http_headers", StringComparison.Ordinal),
    };

    /// <summary>A TOML basic string's text with its escapes undone (enough for paths: each backslash pair stands for the next character).</summary>
    private static string Unescape(string value)
    {
        var text = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length)
            {
                i++;
            }

            _ = text.Append(value[i]);
        }

        return text.ToString();
    }

    /// <summary>A table header's dotted key, its quotes and any trailing comment dropped.</summary>
    private static string TableKey(string header) =>
        string.Join('.', StripComment(header).Trim('[', ']').Split('.').Select(part => part.Trim().Trim('"', '\'')));

    /// <summary>The line up to a '#' that isn't inside a quoted string.</summary>
    private static string StripComment(string line)
    {
        var quote = '\0';
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quote == '\0' && c == '#')
            {
                return line[..i].TrimEnd();
            }

            if (c is '"' or '\'')
            {
                quote = quote == '\0' ? c : (quote == c ? '\0' : quote);
            }
        }

        return line;
    }

    [GeneratedRegex("""^mcp_servers\s*[.=]""", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RootServers { get; }

    [GeneratedRegex("""^["']?sindri["']?\s*[.=]""", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SindriKey { get; }

    [GeneratedRegex("""^(url|http_headers)\s*=""", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CodexSetting { get; }

    /// <summary>What a pass over a Codex config found: the lines kept, the sindri table's url and http_headers lines, where they go back, and any problem.</summary>
    private sealed class CodexScan
    {
        public List<string> Kept { get; } = [];

        public List<string> Settings { get; } = [];

        public int Header { get; private set; } = -1;

        public string? Problem { get; set; }

        public void Take(string raw, string line, string table)
        {
            var sindri = string.Equals(table, CodexTable, StringComparison.Ordinal);
            if (sindri && Header < 0 && line.StartsWith('['))
            {
                Kept.Add(raw);
                Header = Kept.Count;
            }
            else if (sindri && CodexSetting.IsMatch(line))
            {
                Settings.Add(line);
            }
            else
            {
                Kept.Add(raw);
            }
        }
    }
}
