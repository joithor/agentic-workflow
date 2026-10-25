using Temporalio.Testing;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>The pinned Temporal dev server, once per test class (real time: the block runtime's bounds and heartbeats run on the wall clock), with Sindri's search attributes.</summary>
public sealed class BlockRuntimeDevServer : IAsyncLifetime
{
    private WorkflowEnvironment? _environment;

    /// <summary>The started environment.</summary>
    public WorkflowEnvironment Environment => _environment ?? throw new InvalidOperationException("the server has not started");

    /// <inheritdoc/>
    public async ValueTask InitializeAsync() => _environment = await WorkflowsHarness.StartDevServer(ct: TestContext.Current.CancellationToken);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_environment is not null)
        {
            await _environment.ShutdownAsync();
        }
    }
}
