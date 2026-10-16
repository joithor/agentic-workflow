namespace Aw.Sindri.Core.Sessions;

/// <summary>The only environment names the collector keeps (collector spec §4.1 step 5); every other value is dropped as it is parsed and never logged.</summary>
public static class EnvironmentAllowlist
{
    /// <summary>The names.</summary>
    public static IReadOnlySet<string> Names { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Keeps the allowlisted entries.</summary>
    /// <param name="all">The parsed name and value pairs.</param>
    /// <returns>Only the allowlisted names.</returns>
    public static IReadOnlyDictionary<string, string> Filter(IEnumerable<KeyValuePair<string, string>> all) => throw new NotSupportedException();
}
