using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>The dev-server adapter against the contract, with the pinned 'temporal' CLI in a temp home.</summary>
[Lane(TestLane.Integration)]
public sealed class DevServerTemporalContractTests : TemporalServerContract
{
    private readonly TempHome _home = new();

    protected override ITemporalServer Create() =>
        new DevServerTemporal(new TemporalSettings(_home.Path, PinnedTemporal.Path()), new TemporalDevServer(ChildEnvironment.Start));

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        _home.Dispose();
    }
}
