using Aw.Sindri.Core.Control;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// The contract every <see cref="ITemporalServer"/> honours (engine spec rev 8 §0 "Operations"): one server per home,
/// ready at the pin with the 'sindri' namespace once Ensure returns; a second Ensure adopts it; Stop takes it down.
/// Run against the fake and, with the pinned 'temporal' CLI, the dev-server adapter.
/// </summary>
public abstract class TemporalServerContract : IAsyncLifetime
{
    private ITemporalServer? _server;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ITemporalServer Server => _server ?? throw new InvalidOperationException("InitializeAsync didn't run");

    /// <summary>A server over a fresh home with nothing running.</summary>
    /// <returns>The server.</returns>
    protected abstract ITemporalServer Create();

    /// <inheritdoc/>
    public ValueTask InitializeAsync()
    {
        _server = Create();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public virtual async ValueTask DisposeAsync()
    {
        if (_server is not null)
        {
            _ = await _server.Stop(CancellationToken.None);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>Ensure starts a server that answers at the pin with the namespace.</summary>
    /// <returns>The test.</returns>
    [Fact(Timeout = 60_000)]
    public async Task Ensure_starts_a_server_that_answers_at_the_pin_with_the_namespace()
    {
        var started = (await Server.Ensure(Token)).Value;

        var facts = await Server.Inspect(Token);

        Expect.Equal(started, facts.Running);
        Expect.True(facts.Reachable && facts.NamespacePresent);
        Expect.Equal(facts.Pinned, facts.Version);
    }

    /// <summary>A second Ensure adopts the running server instead of starting another.</summary>
    /// <returns>The test.</returns>
    [Fact(Timeout = 60_000)]
    public async Task A_second_ensure_adopts_the_running_server()
    {
        var first = (await Server.Ensure(Token)).Value;

        var second = (await Server.Ensure(Token)).Value;

        Expect.Equal(first.Pid, second.Pid);
    }

    /// <summary>Stop takes the server down; with none running it is a no-op.</summary>
    /// <returns>The test.</returns>
    [Fact(Timeout = 60_000)]
    public async Task Stop_takes_it_down_and_inspect_finds_none()
    {
        _ = (await Server.Ensure(Token)).Value;

        Expect.True((await Server.Stop(Token)).IsSuccess);
        Expect.True((await Server.Stop(Token)).IsSuccess);

        Expect.True((await Server.Inspect(Token)).Running is null);
    }
}
