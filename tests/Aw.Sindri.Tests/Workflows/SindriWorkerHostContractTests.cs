using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Host;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Temporalio.Testing;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>The SDK worker host serve runs against the port's contract, on a pinned dev server.</summary>
[Lane(TestLane.Integration)]
public sealed class SindriWorkerHostContractTests : TemporalWorkerContract
{
    private readonly TempHome _home = new();
    private WorkflowEnvironment? _env;
    private ServiceProvider? _sindri;

    protected override async ValueTask<(ITemporalWorker Worker, TemporalEndpoint Frontend)> Create()
    {
        _env = await WorkflowsHarness.StartDevServer(ct: TestContext.Current.CancellationToken);
        _sindri = Composition.Compose(new ServiceCollection(), name => name == "AW_SINDRI_HOME" ? _home.Path : null, new FakeTimeProvider())
            .BuildServiceProvider();
        var port = int.Parse(_env.Client.Connection.Options.TargetHost!.Split(':')[1], System.Globalization.CultureInfo.InvariantCulture);
        return (_sindri.GetRequiredService<ITemporalWorker>(), new TemporalEndpoint(1, port, port + 1));
    }

    public override async ValueTask DisposeAsync()
    {
        if (_sindri is not null)
        {
            await _sindri.DisposeAsync();
        }

        if (_env is not null)
        {
            await _env.DisposeAsync();
        }

        _home.Dispose();
        await base.DisposeAsync();
    }
}
