using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>The in-memory teams pass the same contract as the team-file adapter.</summary>
[Lane(TestLane.Unit)]
public sealed class FakeTeamNamesContractTests : TeamNamesContract
{
    protected override ITeamNames Create() => new FakeTeamNames(new TeamNames("t1", ["api"]), new TeamNames("b", []));

    protected override ITeamNames CreateEmpty() => new FakeTeamNames();
}
