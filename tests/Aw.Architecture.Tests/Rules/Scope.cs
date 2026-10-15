namespace Aw.Architecture.Tests.Rules;

/// <summary>
/// The shaped packages a rule run covers: their root namespaces, which assemblies are their Host or App (by the name each
/// builds; a Host may rename its binary with &lt;AssemblyName&gt;), and which are their Core, Features or Infrastructure.
/// </summary>
/// <param name="Roots">Package root namespaces, e.g. Aw.Judge.</param>
/// <param name="HostAssemblies">Host or small-shape App assembly name → package root.</param>
/// <param name="LayerAssemblies">Core, Features or Infrastructure assembly name → package root and the layer it holds.</param>
public sealed record Scope(
    IReadOnlyList<string> Roots,
    IReadOnlyDictionary<string, string> HostAssemblies,
    IReadOnlyDictionary<string, LayerAssembly> LayerAssemblies)
{
    private static readonly string[] _hostSuffixes = [".Host", ".App"];
    private static readonly (string Suffix, Layer Layer)[] _layers =
        [(".Core", Layer.Core), (".Features", Layer.Features), (".Infrastructure", Layer.Infrastructure)];

    /// <summary>A scope whose assemblies carry their project names (Aw.Judge.Core, Aw.Judge.Host, ...), plus any renamed Hosts.</summary>
    /// <param name="roots">Package root namespaces.</param>
    /// <param name="renamedHosts">Renamed Host or App assembly → package root.</param>
    /// <returns>The scope.</returns>
    public static Scope Of(IEnumerable<string> roots, IEnumerable<KeyValuePair<string, string>>? renamedHosts = null)
    {
        List<string> list = [.. roots];
        var hosts = list.SelectMany(r => _hostSuffixes.Select(s => KeyValuePair.Create(r + s, r)))
            .Concat(renamedHosts ?? [])
            .GroupBy(p => p.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Last().Value, StringComparer.Ordinal);
        var layers = list.SelectMany(r => _layers.Select(l => KeyValuePair.Create(r + l.Suffix, new LayerAssembly(r, l.Layer))))
            .ToDictionary(StringComparer.Ordinal);
        return new Scope(list, hosts, layers);
    }

    /// <summary>
    /// Roots of unshaped packages (the Sindri UI and its heads, spec §4a "UI"): a namespace under one belongs to no shaped
    /// package, even when a shaped root (Aw.Sindri) is a prefix of it, so only the global rules (cycles, naming, service
    /// providers, typed ids) cover it.
    /// </summary>
    public IReadOnlyList<string> Unshaped { get; init; } = [];

    /// <summary>The package root whose Host or App assembly this is, or null.</summary>
    /// <param name="assemblyName">An assembly's short name.</param>
    /// <returns>The root, or null.</returns>
    public string? HostRootOf(string assemblyName) => HostAssemblies.GetValueOrDefault(assemblyName);

    /// <summary>The package root and layer of a Core, Features or Infrastructure assembly, or null.</summary>
    /// <param name="assemblyName">An assembly's short name.</param>
    /// <returns>The root and layer, or null.</returns>
    public LayerAssembly? LayerOf(string assemblyName) => LayerAssemblies.TryGetValue(assemblyName, out var layer) ? layer : null;

    /// <summary>The package root whose namespace contains the given one (longest wins), or null.</summary>
    /// <param name="ns">A namespace.</param>
    /// <returns>The root, or null.</returns>
    public string? RootOf(string ns)
    {
        var shaped = Roots.Where(r => ns.StartsWith(r + ".", StringComparison.Ordinal)).MaxBy(r => r.Length);
        var unshaped = Unshaped.Where(r => ns == r || ns.StartsWith(r + ".", StringComparison.Ordinal)).MaxBy(r => r.Length);
        return unshaped is not null && (shaped is null || unshaped.Length > shaped.Length) ? null : shaped;
    }
}
