using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Planning;

namespace Aw.CodeIndex.Tests.Planning;

public sealed class BuildPlannerTests
{
    private const string Structure = "p:11111111";
    private static readonly DateTimeOffset _at = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private static readonly Dictionary<LayerName, string> _wanted = new()
    {
        [LayerName.Structure] = Structure,
        [LayerName.Clones] = "c1",
        [LayerName.Deps] = "d1",
        [LayerName.Graph] = "g:abc",
        [LayerName.Embeddings] = "m1",
    };

    [Fact, Trait("Lane", "unit")]
    public void A_first_build_parses_every_file_and_no_layer_is_current()
    {
        var plan = BuildPlanner.Plan(Input(snapshot: [File("a.cs", "1"), File("b.cs", "2")], stored: [], layers: []));

        Assert.Equal(["a.cs", "b.cs"], plan.ToParse.Select(f => f.Path), StringComparer.Ordinal);
        Assert.Empty(plan.ToDrop);
        Assert.All(plan.LayersCurrent.Values, Assert.False);
        Assert.Equal(5, plan.LayersCurrent.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unchanged_repo_parses_nothing_and_every_layer_is_current()
    {
        var plan = BuildPlanner.Plan(Input([File("a.cs", "1")], [Stored("a.cs", "1")], AllOk()));

        Assert.Empty(plan.ToParse);
        Assert.Empty(plan.ToDrop);
        Assert.All(plan.LayersCurrent.Values, Assert.True);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_changed_blob_is_parsed_and_the_other_files_are_not()
    {
        var plan = BuildPlanner.Plan(Input([File("a.cs", "1"), File("b.cs", "3")], [Stored("a.cs", "1"), Stored("b.cs", "2")], AllOk()));

        Assert.Equal("b.cs", Assert.Single(plan.ToParse).Path);
        Assert.Empty(plan.ToDrop);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_new_file_is_parsed()
    {
        var plan = BuildPlanner.Plan(Input([File("a.cs", "1"), File("new.cs", "9")], [Stored("a.cs", "1")], AllOk()));

        Assert.Equal("new.cs", Assert.Single(plan.ToParse).Path);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_file_that_left_the_snapshot_is_dropped()
    {
        var plan = BuildPlanner.Plan(Input([File("a.cs", "1")], [Stored("a.cs", "1"), Stored("z.cs", "2"), Stored("b.cs", "2")], AllOk()));

        Assert.Empty(plan.ToParse);
        Assert.Equal(["b.cs", "z.cs"], plan.ToDrop);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_changed_structure_stamp_parses_everything_and_still_drops_what_left()
    {
        var wanted = new Dictionary<LayerName, string>(_wanted) { [LayerName.Structure] = "p:22222222" };

        var plan = BuildPlanner.Plan(Input([File("a.cs", "1"), File("b.cs", "2")], [Stored("a.cs", "1"), Stored("b.cs", "2"), Stored("gone.cs", "3")], AllOk(), wanted));

        Assert.Equal(["a.cs", "b.cs"], plan.ToParse.Select(f => f.Path), StringComparer.Ordinal);
        Assert.Equal(["gone.cs"], plan.ToDrop);
        Assert.False(plan.LayersCurrent[LayerName.Structure]);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_empty_repo_drops_every_stored_file()
    {
        var plan = BuildPlanner.Plan(Input([], [Stored("a.cs", "1"), Stored("b.cs", "2")], AllOk()));

        Assert.Empty(plan.ToParse);
        Assert.Equal(["a.cs", "b.cs"], plan.ToDrop);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_file_stored_as_not_parsed_is_not_parsed_again_while_its_blob_is_the_same()
    {
        var plan = BuildPlanner.Plan(Input([File("big.cs", "1")], [new StoredFile("big.cs", "1", Parsed: false)], AllOk()));

        Assert.Empty(plan.ToParse);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_new_graph_stamp_alone_makes_only_the_graph_not_current()
    {
        var wanted = new Dictionary<LayerName, string>(_wanted) { [LayerName.Graph] = "g:def" };

        var plan = BuildPlanner.Plan(Input([File("a.cs", "1")], [Stored("a.cs", "1")], AllOk(), wanted));

        Assert.Empty(plan.ToParse);
        Assert.False(plan.LayersCurrent[LayerName.Graph]);
        Assert.True(plan.LayersCurrent[LayerName.Structure]);
        Assert.True(plan.LayersCurrent[LayerName.Embeddings]);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(LayerState.Ok, true)]
    [InlineData(LayerState.Disabled, true)]
    [InlineData(LayerState.Unavailable, false)]
    [InlineData(LayerState.Pending, false)]
    public void A_layer_is_current_only_when_it_is_ok_or_disabled(LayerState state, bool current)
    {
        var layers = AllOk().Select(l => l.Layer == LayerName.Embeddings ? l with { State = state } : l).ToList();

        var plan = BuildPlanner.Plan(Input([File("a.cs", "1")], [Stored("a.cs", "1")], layers));

        Assert.Equal(current, plan.LayersCurrent[LayerName.Embeddings]);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_changed_file_makes_every_layer_not_current()
    {
        var plan = BuildPlanner.Plan(Input([File("a.cs", "2")], [Stored("a.cs", "1")], AllOk()));

        Assert.All(plan.LayersCurrent.Values, Assert.False);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_layer_with_no_wanted_stamp_is_never_current()
    {
        var wanted = new Dictionary<LayerName, string>(_wanted.Where(p => p.Key != LayerName.Deps));

        var plan = BuildPlanner.Plan(Input([File("a.cs", "1")], [Stored("a.cs", "1")], AllOk(), wanted));

        Assert.False(plan.LayersCurrent[LayerName.Deps]);
    }

    private static BuildPlanInput Input(IReadOnlyList<SnapshotFile> snapshot, IReadOnlyList<StoredFile> stored, IReadOnlyList<LayerRecord> layers, IReadOnlyDictionary<LayerName, string>? wanted = null)
        => new(snapshot, stored, layers, wanted ?? _wanted);

    private static SnapshotFile File(string path, string blob) => new(path, blob, Size: 10);

    private static StoredFile Stored(string path, string blob) => new(path, blob, Parsed: true);

    private static List<LayerRecord> AllOk()
        => [.. _wanted.Select(p => new LayerRecord(p.Key, p.Value, LayerState.Ok, Code: null, Detail: "", _at))];
}
