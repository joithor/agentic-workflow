namespace Aw.Architecture.Tests.Rules;

/// <summary>
/// The shaped packages a rule run covers: their root namespaces, which assemblies are their Host or App (by the name each
/// builds; a Host may rename its binary with &lt;AssemblyName&gt;), and which are their Core, Features or Infrastructure.
/// </summary>
/// <param name="Roots">Package root namespaces, e.g. Aw.Judge.</param>
/// <param name="HostAssemblies">Host or small-shape App assembly name → package root.</param>
/// <param name="LayerAssemblies">Core, Features or Infrastructure assembly name → package root.</param>
public sealed record Scope(
    IReadOnlyList<string> Roots,
    IReadOnlyDictionary<string, string> HostAssemblies,
    IReadOnlyDictionary<string, string> LayerAssemblies)
{
    private static readonly string[] _hostSuffixes = [".Host", ".App"];
    private static readonly string[] _layerSuffixes = [".Core", ".Features", ".Infrastructure"];

    /// <summary>A scope whose assemblies carry their project names (Aw.Judge.Core, Aw.Judge.Host, ...), plus any renamed Hosts.</summary>
    /// <param name="roots">Package root namespaces.</param>
    /// <param name="renamedHosts">Renamed Host or App assembly → package root.</param>
    /// <returns>The scope.</returns>
    public static Scope Of(IEnumerable<string> roots, IEnumerable<KeyValuePair<string, string>>? renamedHosts = null)
    {
        List<string> list = [.. roots];
        return new Scope(list, Map(list, _hostSuffixes, renamedHosts ?? []), Map(list, _layerSuffixes, []));
    }

    /// <summary>The package root whose Host or App assembly this is, or null.</summary>
    /// <param name="assemblyName">An assembly's short name.</param>
    /// <returns>The root, or null.</returns>
    public string? HostRootOf(string assemblyName) => HostAssemblies.GetValueOrDefault(assemblyName);

    /// <summary>The package root whose Core, Features or Infrastructure assembly this is, or null.</summary>
    /// <param name="assemblyName">An assembly's short name.</param>
    /// <returns>The root, or null.</returns>
    public string? LayerRootOf(string assemblyName) => LayerAssemblies.GetValueOrDefault(assemblyName);

    /// <summary>The package root whose namespace contains the given one (longest wins), or null.</summary>
    /// <param name="ns">A namespace.</param>
    /// <returns>The root, or null.</returns>
    public string? RootOf(string ns) => Roots.Where(r => ns.StartsWith(r + ".", StringComparison.Ordinal)).MaxBy(r => r.Length);

    private static Dictionary<string, string> Map(List<string> roots, string[] suffixes, IEnumerable<KeyValuePair<string, string>> extra) =>
        roots.SelectMany(r => suffixes.Select(s => KeyValuePair.Create(r + s, r)))
            .Concat(extra)
            .GroupBy(p => p.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Last().Value, StringComparer.Ordinal);
}
