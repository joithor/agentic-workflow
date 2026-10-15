namespace Aw.Meta.Core.Health;

/// <summary>Recognizes a leaked scratch workflow server.</summary>
public static class OrphanRule
{
    /// <summary>The age a process must exceed to count as leaked, in seconds.</summary>
    public const long MinimumAgeSeconds = 300;

    /// <summary>Whether <paramref name="process"/> is a leaked temporal dev server.</summary>
    /// <param name="process">The process.</param>
    /// <param name="parentCmdline">The parent's command line, when it could be read.</param>
    /// <returns>True when it is a temporal "server start-dev" with a --db-filename under /tmp/aw-sindri-, whose parent is "systemd --user" or init, older than five minutes.</returns>
    public static bool IsOrphan(ProcessInfo process, string? parentCmdline) => throw new NotSupportedException("OrphanRule: make OrphanRuleTests pass");
}
