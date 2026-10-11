using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Scripts;

/// <summary>The page's URL path is <paramref name="Path"/>.</summary>
/// <param name="Path">The expected path (not empty).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UrlPath(string Path) : ExpectedState
{
    /// <inheritdoc/>
    public override string Describe() => $"url path {Path}";

    /// <inheritdoc/>
    public override string? Problem() => Path.Length == 0 ? "url-path needs a path" : null;
}
