using Aw.Meta.Core;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Ports;
using Xunit;

namespace Aw.Meta.Testing;

/// <summary>The IMetaConfig contract: the same tests run against the in-memory fake and every real adapter.</summary>
public abstract class MetaConfigContract
{
    private static readonly CancellationToken _none = CancellationToken.None;

    /// <summary>A config store with nothing saved.</summary>
    /// <returns>The store.</returns>
    protected abstract IMetaConfig Create();

    /// <summary>With nothing saved, loading is AW-META-002.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Loading_with_no_config_says_so()
    {
        var loaded = await Create().Load(_none);

        Expect.That(loaded.IsError && loaded.Error.Code == MetaErrors.NoTeamConfig.Code, "AW-META-002");
    }

    /// <summary>A saved config loads back with every field, optional ones included.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_saved_config_loads_back_with_every_field()
    {
        var store = Create();
        var config = TeamConfigs.Full();

        var saved = await store.Save(config, _none);
        var loaded = await store.Load(_none);

        Expect.That(saved.IsSuccess && loaded.IsSuccess && TeamConfigs.Same(config, loaded.Value), "it round-trips, prompts and log included");
    }

    /// <summary>A config with only the required fields loads with the defaults.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_minimal_config_keeps_its_defaults()
    {
        var store = Create();
        _ = await store.Save(new TeamConfig("t", "/r", ["a"]), _none);

        var loaded = await store.Load(_none);

        Expect.That(loaded.IsSuccess && loaded.Value.MetaLane == "meta" && loaded.Value.Log is null && loaded.Value.Prompts is null, "defaults");
        Expect.That(loaded.Value.Gate.Scopes.Count == 0 && loaded.Value.MergeOrder.Count == 0 && loaded.Value.KnownFlakes.Count == 0, "empty gate and lists");
    }
}
