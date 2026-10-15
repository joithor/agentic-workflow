namespace Aw.Meta.Core.Worktrees;

/// <summary>Reads the output of "git worktree list --porcelain".</summary>
internal static class PorcelainWorktrees
{
    private const string BranchPrefix = "refs/heads/";

    /// <summary>Parses the blocks, one per worktree, the first being the main worktree.</summary>
    /// <param name="output">The porcelain output.</param>
    /// <returns>The worktrees in git's order.</returns>
    internal static IReadOnlyList<WorktreeEntry> Parse(string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        List<WorktreeEntry> entries = [];
        foreach (var block in output.ReplaceLineEndings("\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Block(block, entries.Count == 0) is { } entry)
            {
                entries.Add(entry);
            }
        }

        return entries;
    }

    private static WorktreeEntry? Block(string block, bool first)
    {
        var parsed = new Fields(Path: null, Head: string.Empty, Branch: null, Locked: false, LockReason: null, Prunable: false);
        foreach (var line in block.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var (key, value) = Field(line);
            parsed = Apply(parsed, key, value);
        }

        return parsed.Path is null ? null : new WorktreeEntry(parsed.Path, parsed.Head, parsed.Branch, first, parsed.Locked, parsed.LockReason, parsed.Prunable);
    }

    private static Fields Apply(Fields fields, string key, string value) => key switch
    {
        "worktree" => fields with { Path = value },
        "HEAD" => fields with { Head = value },
        "branch" => fields with { Branch = value.StartsWith(BranchPrefix, StringComparison.Ordinal) ? value[BranchPrefix.Length..] : value },
        "locked" => fields with { Locked = true, LockReason = value.Length > 0 ? value : null },
        "prunable" => fields with { Prunable = true },
        _ => fields,
    };

    private sealed record Fields(string? Path, string Head, string? Branch, bool Locked, string? LockReason, bool Prunable);

    private static (string Key, string Value) Field(string line)
    {
        var space = line.IndexOf(' ', StringComparison.Ordinal);
        return space < 0 ? (line, string.Empty) : (line[..space], line[(space + 1)..]);
    }
}
