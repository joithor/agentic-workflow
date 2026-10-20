using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>The 'index.graph' keys (code index spec §13).</summary>
/// <param name="Enabled">Off leaves the graph layer disabled (default true).</param>
/// <param name="MaxMB">The largest graph file read (default <see cref="DefaultMaxMB"/>).</param>
/// <param name="MaxNodes">The most graph nodes read; edges are capped at four times that (default <see cref="DefaultMaxNodes"/>).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GraphSettings(bool? Enabled = null, int? MaxMB = null, int? MaxNodes = null)
{
    /// <summary>The default for 'maxMB'.</summary>
    public const int DefaultMaxMB = 256;

    /// <summary>The default for 'maxNodes'.</summary>
    public const int DefaultMaxNodes = 500_000;
}
