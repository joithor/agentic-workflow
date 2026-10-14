using Aw.Context.Core.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.PurgeMap;

public sealed class PurgeMapTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task Purge_deletes_expired_exports_and_versions_past_the_last_20_and_keeps_the_rest()
    {
        for (var i = 0; i < 23; i++)
        {
            _ = await _world.ImportFixture();
        }

        var old = (await _world.Map.ExportMap("old.json", MapWorld.Human, MapWorld.None)).Value.Path;
        var fresh = (await _world.Map.ExportMap("fresh.json", MapWorld.Human, MapWorld.None)).Value.Path;
        _world.Exports.Touch(old, _world.Clock.GetUtcNow().AddDays(-14).AddSeconds(-1));
        _world.Exports.Touch(fresh, _world.Clock.GetUtcNow().AddDays(-14));

        var report = (await _world.Map.PurgeExpired(MapWorld.None)).Value;

        Assert.Equal(new PurgeReport(Exports: 1, Versions: 2), report);
        Assert.Equal(fresh, Assert.Single(_world.Exports.Documents.Keys));
        var versions = (await _world.Store.Versions(MapWorld.None)).Value;
        Assert.Equal(21, versions.Count);
        Assert.Equal(23, versions[0].Number);
        Assert.Equal(MapHealthState.Ok, (await _world.Map.CheckRetention(MapWorld.None)).Value.State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Purge_with_nothing_to_do_reports_zero() =>
        Assert.Equal(new PurgeReport(Exports: 0, Versions: 0), (await _world.Map.PurgeExpired(MapWorld.None)).Value);
}
