using Aw.Kernel.Testing;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>The in-memory repo checkouts honour the checkout contract.</summary>
[Lane(TestLane.Unit)]
public sealed class InMemoryRepoCheckoutsContractTests : RepoCheckoutsContract
{
    protected override IRepoCheckouts Create() => new InMemoryRepoCheckouts().With("web", "/work/web");
}
