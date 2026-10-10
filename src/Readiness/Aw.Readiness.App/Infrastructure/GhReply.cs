using System.Globalization;

namespace Aw.Readiness.App.Infrastructure;

/// <summary>What one gh call printed. Exit -1 means it never ran (missing binary, timeout).</summary>
/// <param name="ExitCode">gh's exit code.</param>
/// <param name="Stdout">Its standard output (gh prints the API's JSON body there, also on HTTP errors).</param>
/// <param name="Stderr">Its standard error, trimmed ("gh: … (HTTP 404)").</param>
internal sealed record GhReply(int ExitCode, string Stdout, string Stderr)
{
    internal bool Ok => ExitCode == 0;

    internal bool Ran => ExitCode >= 0;

    /// <summary>The first line of stderr, for evidence.</summary>
    internal string Reason => Stderr.Split('\n')[0];

    internal bool IsHttp(int status) => Stderr.Contains(string.Create(CultureInfo.InvariantCulture, $"(HTTP {status})"), StringComparison.Ordinal);

    internal static GhReply Failed(string reason) => new(-1, "", reason);
}
