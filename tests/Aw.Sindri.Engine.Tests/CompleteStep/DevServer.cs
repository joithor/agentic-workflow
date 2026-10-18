using Aw.Sindri.Tests.Workflows;
using Temporalio.Testing;

namespace Aw.Sindri.Tests.CompleteStep;

/// <summary>One pinned dev server ('temporal', StartLocalAsync) per test class, with Sindri's namespace and search attributes.</summary>
public sealed class DevServer : IAsyncLifetime
{
    private WorkflowEnvironment? _environment;

    /// <summary>The started server.</summary>
    public WorkflowEnvironment Environment => _environment ?? throw new InvalidOperationException("the dev server has not started");

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
