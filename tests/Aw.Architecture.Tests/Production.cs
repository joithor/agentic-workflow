using System.Reflection;
using ArchUnitNET.Loader;
using Aw.Architecture.Tests.Rules;
using ArchArchitecture = ArchUnitNET.Domain.Architecture;

namespace Aw.Architecture.Tests;

/// <summary>The registered production assemblies (Packages.cs), loaded once for every rule.</summary>
internal static class Production
{
    private static readonly Lazy<ArchArchitecture> _loaded = new(Load);

    /// <summary>The registered assemblies, loaded by name from the test's output directory.</summary>
    public static IReadOnlyList<Assembly> Assemblies { get; } = [.. Packages.Production.Select(TryLoad).OfType<Assembly>()];

    private static readonly string[] _layerProjects = ["Core", "Features", "Infrastructure", "Host", "App"];

    private static ArchArchitecture Load()
    {
        var loader = new ArchLoader().LoadAssemblies([.. Assemblies]);
        return loader.Build();
    }

    private static Assembly? TryLoad(string name)
    {
        try
        {
            return Assembly.Load(name);
        }
        catch (FileNotFoundException)
        {
            return null; // RegistryTests reports it (ARCH-REG-002).
        }
    }

    /// <summary>The ArchUnitNET model of the registered assemblies.</summary>
    public static ArchArchitecture Architecture => _loaded.Value;

    /// <summary>
    /// The shaped package roots: Aw.Judge for Aw.Judge.Core, Aw.Judge.Features, ... and Aw.Judge.App. Tier-0 packages
    /// (Aw.Kernel, Aw.Kernel.Data) have no layers, so only the cycle, naming and service-provider rules cover them.
    /// </summary>
    public static IReadOnlyList<string> Roots { get; } = RootsOf(Packages.Production);

    /// <summary>The package roots a list of assembly names implies.</summary>
    /// <param name="assemblyNames">Assembly names.</param>
    /// <returns>Distinct roots, sorted.</returns>
    public static IReadOnlyList<string> RootsOf(IEnumerable<string> assemblyNames) =>
        [.. assemblyNames
            .Select(n => n.Split('.'))
            .Where(s => s.Length > 2 && _layerProjects.Contains(s[^1], StringComparer.Ordinal))
            .Select(s => string.Join('.', s[..^1]))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

    /// <summary>Every dependency edge among the registered assemblies' types.</summary>
    public static IReadOnlyList<Edge> Edges => Edge.All(Architecture.Types, Roots);
}
