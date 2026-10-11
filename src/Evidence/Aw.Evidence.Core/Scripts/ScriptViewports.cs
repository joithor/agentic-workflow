using Aw.Evidence.Core.Geometry;

namespace Aw.Evidence.Core.Scripts;

/// <summary>The size of each named viewport.</summary>
public static class ScriptViewports
{
    /// <summary>The viewport size for <paramref name="kind"/>: desktop 1280x800, phone 375x812.</summary>
    /// <param name="kind">The named viewport.</param>
    /// <returns>Its size.</returns>
    public static ViewportSize SizeOf(ViewportKind kind) => kind == ViewportKind.Phone ? new(375, 812) : new(1280, 800);

    /// <summary>The name a viewport is written with in run files and file names.</summary>
    /// <param name="kind">The named viewport.</param>
    /// <returns>'desktop' or 'phone'.</returns>
    public static string NameOf(ViewportKind kind) => kind == ViewportKind.Phone ? "phone" : "desktop";
}
