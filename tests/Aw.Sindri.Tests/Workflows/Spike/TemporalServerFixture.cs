using Temporalio.Testing;

namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>One pinned Temporal server per test class (ExistingPath from binaries.lock; the SDK never downloads).</summary>
public abstract class TemporalServerFixture : IAsyncLifetime
{
    private WorkflowEnvironment? _environment;

    /// <summary>The server this fixture runs.</summary>
    public abstract ServerKind Kind { get; }

    /// <summary>The started environment.</summary>
    public WorkflowEnvironment Environment => _environment ?? throw new InvalidOperationException("the server has not started");

    /// <inheritdoc/>
    public async ValueTask InitializeAsync() => _environment = await Start(Kind);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_environment is not null)
        {
            await _environment.ShutdownAsync();
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>Starts <paramref name="kind"/> from its pinned binary.</summary>
    /// <param name="kind">The server.</param>
    /// <returns>The environment.</returns>
    public static async Task<WorkflowEnvironment> Start(ServerKind kind) => kind switch
    {
        ServerKind.TimeSkipping => await WorkflowEnvironment.StartTimeSkippingAsync(new WorkflowEnvironmentStartTimeSkippingOptions
        {
            TestServer = new TestServerOptions { ExistingPath = PinnedBinaries.Path("temporal-test-server") },
        }),
        ServerKind.Local => await WorkflowEnvironment.StartLocalAsync(new WorkflowEnvironmentStartLocalOptions
        {
            UI = false,
            DevServerOptions = new DevServerOptions { ExistingPath = PinnedBinaries.Path("temporal"), LogLevel = "error" },
        }),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "no such server"),
    };
}
