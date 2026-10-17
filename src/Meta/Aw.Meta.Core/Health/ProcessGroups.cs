namespace Aw.Meta.Core.Health;

/// <summary>Sorts the host's processes into the kinds that matter for memory pressure.</summary>
public static class ProcessGroups
{
    /// <summary>The group a process belongs to.</summary>
    public const string Claude = "claude";

    /// <summary>The dotnet host, MSBuild and the compiler servers.</summary>
    public const string Dotnet = "dotnet";

    /// <summary>The Temporal dev server.</summary>
    public const string Temporal = "temporal";

    /// <summary>The MCP servers sessions start.</summary>
    public const string Mcp = "mcp";

    private static readonly string[] _order = [Claude, Dotnet, Temporal, Mcp, "docker/qemu", "onyx/opensearch/vespa", "node", "other"];
    private static readonly string[] _mcpMarkers = ["mcp", "serena", "context7"];
    private static readonly string[] _containerMarkers = ["docker", "qemu", "vpnkit", "containerd"];
    private static readonly string[] _searchMarkers = ["onyx", "opensearch", "vespa"];

    /// <summary>The group of one process, by its program and command line.</summary>
    /// <param name="process">The process.</param>
    /// <returns>The group's name.</returns>
    public static string Of(ProcessInfo process)
    {
        ArgumentNullException.ThrowIfNull(process);
        var command = process.Cmdline;
        var program = Program(command);
        var lower = command.ToLowerInvariant();
        return program switch
        {
            "claude" => Claude,
            "dotnet" or "msbuild" or "vbcscompiler" => Dotnet,
            _ when lower.Contains("msbuild.dll", StringComparison.Ordinal) || lower.Contains("vbcscompiler.dll", StringComparison.Ordinal) => Dotnet,
            _ when program.Contains("temporal", StringComparison.Ordinal) => Temporal,
            _ when _mcpMarkers.Any(m => lower.Contains(m, StringComparison.Ordinal)) => Mcp,
            _ when _containerMarkers.Any(m => program.Contains(m, StringComparison.Ordinal)) => "docker/qemu",
            _ when _searchMarkers.Any(m => lower.Contains(m, StringComparison.Ordinal)) => "onyx/opensearch/vespa",
            "node" => "node",
            _ => "other",
        };
    }

    /// <summary>Counts and sizes the groups.</summary>
    /// <param name="processes">The processes.</param>
    /// <returns>The groups that have a process, in a fixed order.</returns>
    public static IReadOnlyList<ProcessGroup> Group(IReadOnlyList<ProcessInfo> processes)
    {
        ArgumentNullException.ThrowIfNull(processes);
        var byGroup = processes.ToLookup(Of, StringComparer.Ordinal);
        return [.. _order.Where(name => byGroup[name].Any()).Select(name => new ProcessGroup(name, byGroup[name].Count(), byGroup[name].Sum(p => p.RssKb) / 1024))];
    }

    // The program's file name in lower case; a node-run claude (node .../claude) counts as claude.
    private static string Program(string cmdline)
    {
        var words = cmdline.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var first = words.Length > 0 ? Name(words[0]) : string.Empty;
        return first == "node" && words.Length > 1 && Name(words[1]) == "claude" ? "claude" : first;
    }

    private static string Name(string path) => path[(path.LastIndexOf('/') + 1)..].ToLowerInvariant();
}
