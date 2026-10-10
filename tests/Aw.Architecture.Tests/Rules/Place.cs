using ArchUnitNET.Domain;

namespace Aw.Architecture.Tests.Rules;

/// <summary>A type's package root, layer, slice and whether it is the composition root.</summary>
/// <param name="Root">The package's root namespace, e.g. Aw.Judge.</param>
/// <param name="Layer">The layer the namespace names.</param>
/// <param name="Slice">The feature slice (the folder under Features), or null outside Features.</param>
/// <param name="IsComposition">True for the Composition type in Host (or App in the small shape).</param>
public sealed record Place(string Root, Layer Layer, string? Slice, bool IsComposition)
{
    private static readonly string[] _hostAssemblySuffixes = [".Host", ".App"];

    /// <summary>
    /// The type's place, or null when it isn't in a shaped package (a tier-0 package such as Aw.Kernel) or sits in a
    /// namespace under a package root that names no layer (ARCH-PLACE-001 reports those). A type outside every root
    /// that lives in a package's Host or App assembly (the top-level-statement Program, generator output) is Host.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="roots">The package roots in scope.</param>
    /// <returns>The place, or null.</returns>
    public static Place? Of(IType type, IReadOnlyList<string> roots)
    {
        var ns = TypeNames.NamespaceOf(type);
        var root = RootOf(ns, roots);
        if (root is null)
        {
            return HostRootOfAssembly(TypeNames.AssemblyOf(type), roots) is { } hostRoot
                ? new Place(hostRoot, Layer.Host, Slice: null, IsComposition: false)
                : null;
        }

        var segments = ns[(root.Length + 1)..].Split('.');
        var small = segments[0] == "App";
        var layer = LayerOf(small ? (segments.Length > 1 ? segments[1] : "App") : segments[0], small);
        if (layer is null)
        {
            return null;
        }

        var sliceIndex = small ? 2 : 1;
        var slice = layer == Layer.Features && segments.Length > sliceIndex ? segments[sliceIndex] : null;
        var composition = layer == Layer.Host && TypeNames.TopLevel(type) == "Composition";
        return new Place(root, layer.Value, slice, composition);
    }

    /// <summary>The package root whose namespace contains the given one (longest wins), or null.</summary>
    /// <param name="ns">A namespace.</param>
    /// <param name="roots">The package roots in scope.</param>
    /// <returns>The root, or null.</returns>
    public static string? RootOf(string ns, IReadOnlyList<string> roots) =>
        roots.Where(r => ns.StartsWith(r + ".", StringComparison.Ordinal)).MaxBy(r => r.Length);

    /// <summary>The root whose Host (or small-shape App) assembly this is, e.g. Aw.Judge for Aw.Judge.Host; else null.</summary>
    /// <param name="assemblyName">The assembly's short name.</param>
    /// <param name="roots">The package roots in scope.</param>
    /// <returns>The root, or null.</returns>
    public static string? HostRootOfAssembly(string assemblyName, IReadOnlyList<string> roots) =>
        _hostAssemblySuffixes
            .Where(s => assemblyName.EndsWith(s, StringComparison.Ordinal))
            .Select(s => assemblyName[..^s.Length])
            .FirstOrDefault(r => roots.Contains(r, StringComparer.Ordinal));

    private static Layer? LayerOf(string name, bool small) => name switch
    {
        "Core" when !small => Layer.Core,
        "Features" => Layer.Features,
        "Infrastructure" => Layer.Infrastructure,
        "Commands" when small => Layer.Commands,
        "App" when small => Layer.Host,
        "Host" when !small => Layer.Host,
        _ => null,
    };
}
