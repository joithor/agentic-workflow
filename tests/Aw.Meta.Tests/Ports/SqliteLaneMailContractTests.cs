using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Infrastructure.Sqlite;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Adapters;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Integration)]
public sealed class SqliteLaneMailContractTests : LaneMailContract, IDisposable
{
    private readonly TempDirectory _state = new();

    protected override ValueTask<(ILaneMail Mail, IMessageStore Messages)> Create()
    {
        var database = new MetaDatabase(MetaDatabase.OptionsFor(_state.Path), new FakeTimeProvider());
        return ValueTask.FromResult<(ILaneMail, IMessageStore)>((new SqliteLaneMail(database), new SqliteMessageStore(database)));
    }

    public void Dispose() => _state.Dispose();
}
