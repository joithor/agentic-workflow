using Aw.Kernel.Testing;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>The in-memory run proposals honour the run-proposals contract.</summary>
[Lane(TestLane.Unit)]
public sealed class InMemoryScopeRunProposalsContractTests : ScopeRunProposalsContract
{
    protected override IScopeRunProposals Create() => new InMemoryScopeRunProposals();
}
