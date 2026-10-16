using Aw.Context.Core.Linking;
using Aw.Context.Infrastructure.Intent;
using Aw.Context.Infrastructure.Store;
using Aw.Context.Testing.Linking;
using Aw.Context.Tests.Map;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Linking;

[Trait("Lane", "integration")]
public sealed class SqliteIntentStoreContractTests : IntentStoreContract, IDisposable
{
    private readonly TempHome _home = new();
    private readonly List<ContextDatabase> _databases = [];

    public void Dispose()
    {
        _databases.ForEach(d => d.Dispose());
        _home.Dispose();
    }

    protected override IIntentStore CreateStore()
    {
        var database = new ContextDatabase(SettingsFromEnvironment.Read(_home.Environment), new FakeTimeProvider());
        _databases.Add(database);
        return new SqliteIntentStore(database);
    }
}
