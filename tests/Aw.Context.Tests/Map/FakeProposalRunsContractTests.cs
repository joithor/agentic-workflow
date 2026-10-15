using Aw.Context.Core.Proposals;
using Aw.Context.Testing.Map;

namespace Aw.Context.Tests.Map;

[Trait("Lane", "unit")]
public sealed class FakeProposalRunsContractTests : ProposalRunsContract
{
    protected override IProposalRuns CreateRuns() => new FakeGraphStore();
}
