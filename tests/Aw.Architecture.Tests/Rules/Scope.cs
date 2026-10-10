namespace Aw.Architecture.Tests.Rules;

/// <summary>The shaped packages a rule run covers: their root namespaces, and which assemblies are their Host or App.</summary>
/// <param name="Roots">Package root namespaces, e.g. Aw.Judge.</param>
/// <param name="HostAssemblies">Host (or small-shape App) assembly name → package root; a Host that renames its binary with &lt;AssemblyName&gt; maps that name.</param>
public sealed record Scope(IReadOnlyList<string> Roots, IReadOnlyDictionary<string, string> HostAssemblies)
{
    /// <summary>A scope whose Host and App assemblies carry their project names (Aw.Judge.Host, Aw.Judge.App), plus any renamed ones.</summary>
    /// <param name="roots">Package root namespaces.</param>
    /// <param name="renamedHosts">Renamed Host or App assembly → package root.</param>
    /// <returns>The scope.</returns>
    public static Scope Of(IEnumerable<string> roots, IEnumerable<KeyValuePair<string, string>>? renamedHosts = null)
    {
        List<string> list = [.. roots];
        var hosts = list.SelectMany(r => new[] { KeyValuePair.Create(r + ".Host", r), KeyValuePair.Create(r + ".App", r) })
            .Concat(renamedHosts ?? [])
            .GroupBy(p => p.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Last().Value, StringComparer.Ordinal);
        return new Scope(list, hosts);
    }

    /// <summary>The package root whose Host or App assembly this is, or null.</summary>
    /// <param name="assemblyName">An assembly's short name.</param>
    /// <returns>The root, or null.</returns>
    public string? HostRootOf(string assemblyName) => HostAssemblies.GetValueOrDefault(assemblyName);

    /// <summary>The package root whose namespace contains the given one (longest wins), or null.</summary>
    /// <param name="ns">A namespace.</param>
    /// <returns>The root, or null.</returns>
    public string? RootOf(string ns) => Roots.Where(r => ns.StartsWith(r + ".", StringComparison.Ordinal)).MaxBy(r => r.Length);
}
