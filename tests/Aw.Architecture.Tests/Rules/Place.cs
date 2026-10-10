using ArchUnitNET.Domain;

namespace Aw.Architecture.Tests.Rules;

/// <summary>A type's package root, layer, slice and whether it is the composition root.</summary>
/// <param name="Root">The package's root namespace, e.g. Aw.Judge.</param>
/// <param name="Layer">The layer the namespace names.</param>
/// <param name="Slice">The feature slice (the folder under Features), or null outside Features.</param>
/// <param name="IsComposition">True for the Composition type in Host (or App in the small shape).</param>
public sealed record Place(string Root, Layer Layer, string? Slice, bool IsComposition)
{
    /// <summary>
    /// The type's place, or null when it isn't in a shaped package (a tier-0 package such as Aw.Kernel) or sits in a
    /// namespace under a package root that names no layer (ARCH-PLACE-001 reports those). A type outside every root
    /// that lives in a package's Host or App assembly (the top-level-statement Program, generator output) is Host; one in a
    /// Core, Features or Infrastructure assembly gets no place and ARCH-PLACE-001 reports it.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="scope">The shaped packages in scope.</param>
    /// <returns>The place, or null.</returns>
    public static Place? Of(IType type, Scope scope)
    {
        var ns = TypeNames.NamespaceOf(type);
        var root = scope.RootOf(ns);
        if (root is null)
        {
            return scope.HostRootOf(TypeNames.AssemblyOf(type)) is { } hostRoot
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
