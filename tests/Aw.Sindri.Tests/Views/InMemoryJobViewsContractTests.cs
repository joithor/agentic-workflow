using Aw.Kernel.Testing;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Views;

/// <summary>Binds the jobs-view contract to the in-memory fake (API P6 review T9).</summary>
[Lane(TestLane.Unit)]
public sealed class InMemoryJobViewsContractTests : JobViewsContract
{
    protected override ValueTask<IJobViewsSubject> Create() => ValueTask.FromResult<IJobViewsSubject>(new InMemoryJobViewsSubject());
}
