using ArchUnitNET.Domain;

namespace Aw.Architecture.Tests.Rules;

/// <summary>A type's package root, layer, slice and whether it is the composition root.</summary>
/// <param name="Root">The package's root namespace, e.g. Aw.Judge.</param>
/// <param name="Layer">The layer the namespace names.</param>
/// <param name="Slice">The feature slice (the folder under Features), or null outside Features.</param>
/// <param name="IsComposition">True for the Composition type in Host (or App in the small shape).</param>
public sealed record Place(string Root, Layer Layer, string? Slice, bool IsComposition)
{
    /// <summary>The type's place, or null when it isn't in a shaped package (a tier-0 package such as Aw.Kernel).</summary>
    /// <param name="type">The type.</param>
    /// <param name="roots">The package roots in scope.</param>
    /// <returns>The place, or null.</returns>
    public static Place? Of(IType type, IReadOnlyList<string> roots)
    {
        var ns = type.Namespace.FullName;
        var root = roots.Where(r => ns.StartsWith(r + ".", StringComparison.Ordinal)).MaxBy(r => r.Length);
        if (root is null)
        {
            return null;
        }

        var segments = ns[(root.Length + 1)..].Split('.');
        var small = segments[0] == "App";
        var layerName = small ? (segments.Length > 1 ? segments[1] : "App") : segments[0];
        var sliceIndex = small ? 2 : 1;
        var layer = LayerOf(layerName, small, segments);
        if (layer is null)
        {
            return null;
        }

        var slice = layer == Layer.Features && segments.Length > sliceIndex ? segments[sliceIndex] : null;
        var composition = layer == Layer.Host && TypeNames.TopLevel(type) == "Composition";
        return new Place(root, layer.Value, slice, composition);
    }

    private static readonly string[] _hostEntryPoints = ["Commands", "Endpoints"];

    private static Layer? LayerOf(string name, bool small, string[] segments) => name switch
    {
        "Core" when !small => Layer.Core,
        "Features" => Layer.Features,
        "Infrastructure" => Layer.Infrastructure,
        "Commands" when small => Layer.Commands,
        "App" when small => Layer.Host,
        "Host" when !small => segments.Length > 1 && _hostEntryPoints.Contains(segments[1], StringComparer.Ordinal) ? Layer.Commands : Layer.Host,
        _ => null,
    };
}
