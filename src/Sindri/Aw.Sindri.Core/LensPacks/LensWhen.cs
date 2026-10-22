namespace Aw.Sindri.Core.LensPacks;

/// <summary>
/// When a lens applies (line spec §5.2 'when'): always, or when the subject has one of the surface kinds (design), or when one of the changed paths matches
/// one of the globs (review). A lens with both lists applies when either matches.
/// </summary>
/// <param name="SurfaceKinds">The scope's surface kinds that make the lens apply, ignoring case; empty for none.</param>
/// <param name="Paths">The path globs ('**' crosses directories, '*' stays in one) that make the lens apply; empty for none.</param>
public sealed record LensWhen(IReadOnlyList<string> SurfaceKinds, IReadOnlyList<string> Paths)
{
    /// <summary>The lens applies to every subject.</summary>
    public static LensWhen Always { get; } = new([], []);

    /// <summary>Whether this is the 'always' form: no surface kind and no path.</summary>
    public bool IsAlways => SurfaceKinds.Count == 0 && Paths.Count == 0;
}
