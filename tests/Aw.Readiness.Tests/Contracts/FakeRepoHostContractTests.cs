using Aw.Kernel.Testing;
using Aw.Readiness.Core.Ports;
using Aw.Readiness.Testing;

namespace Aw.Readiness.Tests.Contracts;

/// <summary>The repo-host contract bound to the fake (unit lane).</summary>
[Lane(TestLane.Unit)]
public sealed class FakeRepoHostContractTests : RepoHostContract
{
    protected override IRepoHost HostFor(HostScenario scenario) => new FakeRepoHost(HostFactsFor.Scenario(scenario));
}
