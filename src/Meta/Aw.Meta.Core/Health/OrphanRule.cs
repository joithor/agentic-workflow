namespace Aw.Meta.Core.Health;

/// <summary>Recognizes a leaked scratch workflow server.</summary>
public static class OrphanRule
{
    /// <summary>The age a process must exceed to count as leaked, in seconds.</summary>
    public const long MinimumAgeSeconds = 300;

    private const string DbFlag = "--db-filename";
    private const string ScratchPrefix = "/tmp/aw-sindri-";

    /// <summary>Whether <paramref name="process"/> is a leaked temporal dev server.</summary>
    /// <param name="process">The process.</param>
    /// <param name="parentCmdline">The parent's command line, when it could be read.</param>
    /// <returns>True when it is a temporal "server start-dev" with a --db-filename under /tmp/aw-sindri-, whose parent is "systemd --user" or init, older than five minutes.</returns>
    public static bool IsOrphan(ProcessInfo process, string? parentCmdline)
    {
        ArgumentNullException.ThrowIfNull(process);
        var words = process.Cmdline.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return process.AgeSeconds > MinimumAgeSeconds
            && IsTemporalDevServer(process.Cmdline, words)
            && DbFilename(words) is { } db && db.StartsWith(ScratchPrefix, StringComparison.Ordinal)
            && (process.Ppid == 1 || IsUserManager(parentCmdline));
    }

    private static bool IsTemporalDevServer(string cmdline, string[] words) =>
        words.Length > 0
        && words[0][(words[0].LastIndexOf('/') + 1)..].Contains("temporal", StringComparison.Ordinal)
        && cmdline.Contains("server start-dev", StringComparison.Ordinal);

    private static string? DbFilename(string[] words)
    {
        for (var i = 0; i < words.Length; i++)
        {
            if (words[i] == DbFlag)
            {
                return i + 1 < words.Length ? words[i + 1] : null;
            }

            if (words[i].StartsWith(DbFlag + "=", StringComparison.Ordinal))
            {
                return words[i][(DbFlag.Length + 1)..];
            }
        }

        return null;
    }

    private static bool IsUserManager(string? cmdline) =>
        cmdline is not null && cmdline.Trim().Contains("systemd --user", StringComparison.Ordinal);
}
