using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;

namespace Aw.Context.Core.Map;

/// <summary>One map version read in one store session: the version, its graph as stored and every override.</summary>
/// <param name="Version">The version.</param>
/// <param name="Graph">Its graph (overrides not applied).</param>
/// <param name="Overrides">Every override, oldest first.</param>
public sealed record MapSnapshot(MapVersion Version, GraphSnapshot Graph, IReadOnlyList<MapOverride> Overrides)
{
    /// <summary>How answers name the version: 'v12', or 'v12@a41b07d' when it was built at a commit (spec §8.4).</summary>
    public string Label => Version.Commit is { Length: > 0 } commit
        ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"v{Version.Number}@{commit[..Math.Min(7, commit.Length)]}")
        : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"v{Version.Number}");

    /// <summary>The version without its commit: 'v12'.</summary>
    public string Tag => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"v{Version.Number}");
}
