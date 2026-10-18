using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;
using Aw.Context.Tests.Map;
using Aw.Kernel;

namespace Aw.Context.Tests.QueryContext;

/// <summary>The opt-in component hop through the node-shape query (C4): the related feature, the skipped list and the 140 refusal.</summary>
public sealed class ComponentHopQueryTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task With_the_hop_a_feature_sharing_a_code_unit_is_an_affects_item_and_without_it_nothing_is()
    {
        await Import(2);

        var on = (await Query("component")).Value;
        var off = (await Query(hops: null)).Value;

        var reach = Assert.Single(Section(on, "affects").Items);
        Assert.Equal(("feature:f1", 1), (reach.Node, reach.Depth));
        Assert.Equal(["feature:f0", "component code:src/shared.ts", "implements feature:f1"], reach.Path, StringComparer.Ordinal);
        Assert.Null(on.SkippedComponents);
        Assert.Empty(off.Sections.Where(s => s.Name == "affects").SelectMany(s => s.Items));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_unit_over_the_cap_is_listed_in_skipped_components_with_its_feature_count()
    {
        await Import(6);

        var result = (await Query("component")).Value;

        Assert.Equal([new SkippedComponent("code:src/shared.ts", 6)], result.SkippedComponents);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unknown_hop_name_is_refused_with_140()
    {
        await Import(2);

        var result = await Query("neighbors");

        Assert.Equal("SND-CONTEXT-140", result.Error.Code);
    }

    private async Task<Result<ContextResult>> Query(string? hops) =>
        await _world.Engine.Query(new ContextQuery(Text: null, Node: "feature:f0") { Depth = 1, Hops = hops }, MapWorld.Human, MapWorld.None);

    private async Task Import(int features)
    {
        var keys = Enumerable.Range(0, features).Select(i => string.Create(CultureInfo.InvariantCulture, $"feature:f{i}")).ToList();
        var lines = new List<string> { "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:" };
        lines.AddRange(keys.Select(k => $"  - {{key: \"{k}\", label: \"{k[8..]}\"}}"));
        lines.Add("  - {key: \"code:src/shared.ts\", label: \"shared.ts\"}");
        lines.Add("edges:");
        lines.AddRange(keys.Select(k => $"  - {{from: \"code:src/shared.ts\", to: \"{k}\", type: implements}}"));
        _ = await _world.ImportYaml(MapWorld.Yaml([.. lines]));
    }

    private static ResultSection Section(ContextResult result, string name) => result.Sections.Single(s => s.Name == name);
}
