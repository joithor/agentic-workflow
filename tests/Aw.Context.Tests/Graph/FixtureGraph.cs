using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.Graph;

/// <summary>The split-panes fixture map as a reader sees it: checked, with its overrides applied (the tab manager renamed 'Tabs', drag-to-split rejected).</summary>
internal static class FixtureGraph
{
    public static GraphSnapshot Applied(string? yaml = null)
    {
        var map = Maps.Checked(Maps.Parse(yaml ?? FixtureMap.Yaml));
        return OverrideApplier.Apply(map.Graph, map.Overrides).Graph;
    }

    public static NodeKey Key(string value) => NodeKey.From(value);
}
