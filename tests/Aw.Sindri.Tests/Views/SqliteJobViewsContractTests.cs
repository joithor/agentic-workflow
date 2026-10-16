using Aw.Kernel.Testing;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Views;

/// <summary>Binds the jobs-view contract to the SQLite view over a temp views.db (API P6 review T9).</summary>
[Lane(TestLane.Integration)]
public sealed class SqliteJobViewsContractTests : JobViewsContract
{
    protected override ValueTask<IJobViewsSubject> Create() => ValueTask.FromResult<IJobViewsSubject>(new SqliteJobViewsSubject());
}
