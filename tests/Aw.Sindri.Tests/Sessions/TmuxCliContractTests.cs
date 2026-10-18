using System.Runtime.Versioning;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Infrastructure.Sessions;
using Aw.Sindri.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>The tmux adapter passes the tmux servers contract against a private server (tmux -L aw-co2b-&lt;pid&gt;) the test starts and kills itself; skipped without tmux.</summary>
[UnsupportedOSPlatform("windows")]
[Lane(TestLane.Integration)]
public sealed class TmuxCliContractTests : TmuxServersContract, IDisposable
{
    private PrivateTmux? _server;

    protected override string KnownSocket => Server().Socket;

    protected override string UnknownSocket => Path.Combine(SessionFixtures.Root, "no-such-dir", "tmux-socket");

    protected override ITmuxServers Create() => new TmuxCli(new FakeTimeProvider(KnownInstants.Epoch), "tmux", TimeSpan.FromSeconds(5));

    public void Dispose() => _server?.Dispose();

    private PrivateTmux Server()
    {
        _server ??= PrivateTmux.Start();
        Assert.SkipWhen(_server is null, "tmux isn't installed or its private server didn't start");
        return _server;
    }
}
