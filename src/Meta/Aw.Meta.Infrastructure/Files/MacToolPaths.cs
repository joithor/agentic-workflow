namespace Aw.Meta.Infrastructure.Files;

/// <summary>
/// Where the macOS tools run from. Each tool is the absolute path macOS ships it at (so a PATH an agent or a stray directory
/// controls can't substitute another program) when that file exists, and the bare name only when it doesn't.
/// </summary>
/// <param name="Sysctl">The sysctl program.</param>
/// <param name="VmStat">The vm_stat program.</param>
/// <param name="Ps">The ps program.</param>
/// <param name="Kill">The kill program.</param>
/// <param name="Lsof">The lsof program.</param>
internal sealed record MacToolPaths(string Sysctl, string VmStat, string Ps, string Kill, string Lsof)
{
    /// <summary>Resolves every tool.</summary>
    /// <param name="exists">Whether a file exists (File.Exists in the host, a lambda in tests).</param>
    /// <returns>The paths.</returns>
    internal static MacToolPaths Resolve(Func<string, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(exists);
        return new MacToolPaths(
            Pick("/usr/sbin/sysctl", exists), Pick("/usr/bin/vm_stat", exists), Pick("/bin/ps", exists), Pick("/bin/kill", exists), Pick("/usr/sbin/lsof", exists));
    }

    private static string Pick(string absolute, Func<string, bool> exists) =>
        exists(absolute) ? absolute : absolute[(absolute.LastIndexOf('/') + 1)..];
}
