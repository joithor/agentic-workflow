using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;

namespace Aw.Context.Tests.Map;

public sealed class KeyMigrationTests
{
    /// <summary>A synthetic key version 2: layout features move under 'ui.'.</summary>
    private sealed class UiPrefix : IKeyMigration
    {
        public int From => 1;

        public int To => 2;

        public NodeKey Migrate(NodeKey key) =>
            key.Value.StartsWith("feature:layout.", StringComparison.Ordinal) ? NodeKey.From("feature:ui." + key.Name) : key;
    }

    [Fact, Trait("Lane", "unit")]
    public void A_migration_aliases_every_renamed_node_to_its_new_key()
    {
        var graph = Maps.Checked(Maps.Fixture()).Graph;

        var (migrated, version) = KeyMigrations.Apply(graph, 1, [new UiPrefix()]);

        Assert.Equal(2, version);
        var renamed = graph.Nodes.Where(n => n.Key.Value.StartsWith("feature:layout.", StringComparison.Ordinal)).ToList();
        Assert.Equal(4, renamed.Count);
        Assert.All(renamed, n => Assert.Equal("feature:ui." + n.Key.Name, migrated.Resolve(n.Key)?.Key.Value));
        Assert.Equal("feature:ui.layout.split-pane.orientation", migrated.Resolve(Maps.Key("feature:layout.orientation"))?.Key.Value);
        Assert.DoesNotContain(migrated.Edges, e => e.From.Value.StartsWith("feature:layout.", StringComparison.Ordinal) || e.To.Value.StartsWith("feature:layout.", StringComparison.Ordinal));
    }

    // Golden: no override is orphaned by a key migration (spec §6.1).
    [Fact, Trait("Lane", "unit")]
    public void No_override_is_orphaned_by_a_migration()
    {
        var at = DateTimeOffset.Parse("2026-08-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var map = Maps.Checked(Maps.Fixture());
        MapOverride[] overrides =
        [
            .. map.Overrides,
            new(OverrideAction.Rename, Maps.Key("feature:layout.split-pane"), "avery", at, Label: "Splits"),
            new(OverrideAction.Move, Maps.Key("feature:layout.pane-resize"), "avery", at.AddMinutes(1), Other: Maps.Key("feature:tabs.tab-manager")),
            new(OverrideAction.Accept, Maps.Key("feature:layout.split-pane.orientation"), "avery", at.AddMinutes(2)),
        ];
        var before = OverrideApplier.Apply(map.Graph, overrides);

        var (migrated, _) = KeyMigrations.Apply(map.Graph, 1, [new UiPrefix()]);
        var after = OverrideApplier.Apply(migrated, overrides);

        Assert.Empty(before.Orphaned);
        Assert.Empty(after.Orphaned);
        Assert.Equal("Splits", after.Graph.Resolve(Maps.Key("feature:ui.layout.split-pane"))?.Label);
    }

    [Fact, Trait("Lane", "unit")]
    public void Import_runs_the_migration_on_a_file_written_with_older_keys()
    {
        var check = MapDocumentValidator.Check(Maps.Fixture(), [new UiPrefix()], keyVersion: 2);

        Assert.True(check.IsSuccess);
        Assert.NotNull(check.Value.Graph.Resolve(Maps.Key("feature:ui.layout.split-pane")));
        Assert.Equal("feature:ui.layout.split-pane", check.Value.Graph.Resolve(Maps.Key("feature:layout.split-pane"))?.Key.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_file_whose_keys_no_migration_reaches_is_SND_CONTEXT_031()
    {
        var check = MapDocumentValidator.Check(Maps.Fixture(), [], keyVersion: 2);

        Assert.Equal("SND-CONTEXT-031", check.Error.Code);
        Assert.Contains("keyVersion: keys of version 1 can't be migrated to 2", check.Error.Message, StringComparison.Ordinal);
    }
}
