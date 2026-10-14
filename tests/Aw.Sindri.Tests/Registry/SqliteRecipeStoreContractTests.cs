using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Testing.Workflow.Registry;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Registry;

/// <summary>The SQLite recipe store keeps the store contract, on a ledger file in a temp home (engine spec §4.1, §11).</summary>
[Lane(TestLane.Integration)]
public sealed class SqliteRecipeStoreContractTests : RecipeStoreContract, IDisposable
{
    private readonly TempHome _home = new();
    private readonly List<LedgerDatabase> _databases = [];

    public void Dispose()
    {
        _databases.ForEach(d => d.Dispose());
        _home.Dispose();
    }

    protected override IRecipeStore Create()
    {
        var database = new LedgerDatabase(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml")), new FakeTimeProvider());
        _databases.Add(database);
        return new SqliteRecipeStore(database);
    }
}
