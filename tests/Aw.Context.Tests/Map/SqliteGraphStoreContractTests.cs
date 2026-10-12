using Aw.Context.Core.Map;
using Aw.Context.Infrastructure.Graph;
using Aw.Context.Infrastructure.State;
using Aw.Context.Testing.Map;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Map;

[Trait("Lane", "integration")]
public sealed class SqliteGraphStoreContractTests : GraphStoreContract, IDisposable
{
    private readonly TempHome _home = new();
    private readonly List<GraphDatabase> _databases = [];

    public void Dispose()
    {
        _databases.ForEach(d => d.Dispose());
        _home.Dispose();
    }

    protected override IGraphStore CreateStore()
    {
        var database = new GraphDatabase(SettingsFromEnvironment.Read(_home.Environment), new FakeTimeProvider());
        _databases.Add(database);
        return new SqliteGraphStore(database);
    }
}
