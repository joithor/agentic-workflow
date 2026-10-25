using Aw.Kernel.Testing;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Features.Repos;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Tests.Workflow.Blocks.Input;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>The approved profile's repo checkouts honour the checkout contract (the fixture profile maps web at /work/web).</summary>
[Lane(TestLane.Unit)]
public sealed class ProfileRepoCheckoutsContractTests : RepoCheckoutsContract
{
    protected override IRepoCheckouts Create() => new ProfileRepoCheckouts(new ContextInputFixtures.Profiles(ContextInputFixtures.Profile()));
}
