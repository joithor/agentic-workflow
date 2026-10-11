namespace Aw.Evidence.Core.Guard;

/// <summary>A host a run may drive: its bare origin and kind.</summary>
/// <param name="Origin">Scheme, host and port, no trailing slash ('http://localhost:3000').</param>
/// <param name="Kind">Local or preview.</param>
public sealed record ResolvedHost(string Origin, HostKind Kind)
{
    /// <summary>The URL for a same-origin <paramref name="path"/> (one leading '/').</summary>
    /// <param name="path">The path.</param>
    /// <returns>The absolute URL.</returns>
    public string UrlFor(string path) => Origin + path;
}
