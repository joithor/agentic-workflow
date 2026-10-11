using Aw.BugFix.Core.Ports;
using Aw.BugFix.Testing;
using Aw.Kernel.Testing;

namespace Aw.BugFix.Tests.Contracts;

[Lane(TestLane.Unit)]
public sealed class InMemoryStateStoreContractTests : StateStoreContract
{
    private readonly InMemoryStateStore _store = new();

    protected override IStateStore Store => _store;

    protected override string Directory => "/state/x";

    protected override void Corrupt() => _store.Corrupt(Directory);
}
