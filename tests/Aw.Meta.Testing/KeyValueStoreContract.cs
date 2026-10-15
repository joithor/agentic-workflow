using Aw.Meta.Core.Ports;
using Xunit;

namespace Aw.Meta.Testing;

/// <summary>The IKeyValueStore contract: the same tests run against the in-memory fake and every real adapter.</summary>
public abstract class KeyValueStoreContract
{
    private static readonly CancellationToken _none = CancellationToken.None;

    /// <summary>A fresh, empty store.</summary>
    /// <returns>The store.</returns>
    protected abstract ValueTask<IKeyValueStore> Create();

    /// <summary>A key never set reads as null; a set value reads back; a later set replaces it.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Values_read_back_and_a_later_set_replaces_them()
    {
        var store = await Create();

        var before = await store.Get("k", _none);
        _ = await store.Set("k", "one", _none);
        var first = await store.Get("k", _none);
        _ = await store.Set("k", "two", _none);
        var second = await store.Get("k", _none);
        var other = await store.Get("other", _none);

        Expect.That(before.IsSuccess && before.Value is null, "an unset key is null");
        Expect.That(first.Value == "one" && second.Value == "two", "the value reads back and is replaced");
        Expect.That(other.Value is null, "keys are independent");
    }
}
