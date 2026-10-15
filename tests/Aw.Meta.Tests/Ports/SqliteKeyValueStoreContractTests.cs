using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Infrastructure.Sqlite;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Adapters;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Integration)]
public sealed class SqliteKeyValueStoreContractTests : KeyValueStoreContract, IDisposable
{
    private readonly TempDirectory _state = new();

    protected override ValueTask<IKeyValueStore> Create() =>
        ValueTask.FromResult<IKeyValueStore>(new SqliteKeyValueStore(new MetaDatabase(MetaDatabase.OptionsFor(_state.Path), new FakeTimeProvider())));

    public void Dispose() => _state.Dispose();
}
