using Aw.Kernel.Services;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.StartProcess;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Commands;

/// <summary>
/// The CLI's verbs with their real caller headers against a real control plane on loopback (API spec §6, §4.2): a session
/// is refused by the command chain with the CLI's session note, and a human CLI's token passes the caller middleware.
/// </summary>
public sealed class CliEndToEndTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
        _home.Dispose();
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task Session_cli_stop_and_approve_are_SND_AUTH_002_with_the_session_note_and_exit_2()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await Started(ct);
        var hash = (await app.Services.GetRequiredService<IProfileSource>().Load(ct)).Value.Hash;
        await using var scope = app.Services.CreateAsyncScope();
        using var client = Client(app, human: false);
        var verbs = Verbs(app, client, scope.ServiceProvider);

        var stop = await verbs.Stop(ct);
        var approve = await verbs.Approve(hash.Value, interactive: true, new StringReader(hash.Abbreviated + "\n"), ct);

        Assert.Equal((2, 2), (stop, approve));
        var lines = _error.ToString().ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.All(lines, line => Assert.StartsWith("SND-AUTH-002 ", line, StringComparison.Ordinal));
        Assert.All(lines, line => Assert.EndsWith(" (this CLI ran as a session: the per-start human token isn't readable)", line, StringComparison.Ordinal));
        Assert.NotNull(app.Services.GetRequiredService<HeldLease>().Current);   // the refused stop stopped nothing
        await app.StopAsync(ct);
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task Human_cli_sends_the_token_file_and_passes_the_caller_middleware()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await Started(ct);
        var hash = (await app.Services.GetRequiredService<IProfileSource>().Load(ct)).Value.Hash;
        var stale = SliceHarness.Profile("9s").Hash;
        await using var scope = app.Services.CreateAsyncScope();
        using var client = Client(app, human: true);
        var verbs = Verbs(app, client, scope.ServiceProvider);

        var staleCode = await verbs.Approve(stale.Value, interactive: true, new StringReader("approve profile " + stale.Abbreviated + "\n"), ct);
        var staleError = _error.ToString();
        var approved = await verbs.Approve(hash.Value, interactive: true, new StringReader("approve profile " + hash.Abbreviated + "\n"), ct);

        Assert.Equal(2, staleCode);
        Assert.StartsWith("SND-PROFILE-003 ", staleError, StringComparison.Ordinal);   // the service answered: the middleware let the human in
        Assert.Equal(0, approved);
        var recorded = (await app.Services.GetRequiredService<ILedgerStore>().Latest(EventTypes.ProfileApproved, ct)).Value;
        Assert.Equal("human:cli", recorded?.Envelope.Actor);
        await app.StopAsync(ct);
    }

    /// <summary>A control plane in the temp home with a profile, the lease held, this start's token minted to its file and held, and its endpoint recorded.</summary>
    private async Task<WebApplication> Started(CancellationToken ct)
    {
        _ = Directory.CreateDirectory(Path.Combine(_home.Path, "profile"));
        await File.WriteAllTextAsync(Path.Combine(_home.Path, "profile", "profile.yaml"), "schemaVersion: 1\nuser: thor\n", ct);
        var app = LoopbackBinding.Build([], n => n switch { "AW_SINDRI_HOME" => _home.Path, _ => null }, TimeProvider.System, NoTemporal.Use);   // the Temporal side is faked: these tests are about the API
        _ = Pipeline.Configure(app);
        LeaseGrant grant;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            grant = (await scope.ServiceProvider.GetRequiredService<IService<StartProcessRequest, StartProcessResponse>>().Handle(new StartProcessRequest(), ct)).Value.Grant;
        }

        app.Services.GetRequiredService<HeldHumanToken>().Hold((await app.Services.GetRequiredService<IHumanTokenStore>().Mint(ct)).Value);
        await app.StartAsync(ct);
        _ = await app.Services.GetRequiredService<IEndpointFile>().Write(new ControlEndpoint(new Uri(app.Urls.First()), grant.Holder.Pid, grant.Epoch), ct);
        return app;
    }

    /// <summary>The CLI's client with the headers CommandTable gives it: the real token store, no repo.</summary>
    private static ControlPlaneClient Client(WebApplication app, bool human) =>
        new(app.Services.GetRequiredService<IControlPlaneProbe>(), SliceHarness.Json,
            CallerHeaders.For(new AgentSession(Terminal: true, Prover: false), human ? app.Services.GetRequiredService<IHumanTokenStore>() : new InMemoryHumanTokenStore(), static _ => ValueTask.FromResult<Core.Ids.RepoName?>(null)));

    private Verbs Verbs(WebApplication app, ControlPlaneClient client, IServiceProvider scope) =>
        new(client, app.Services.GetRequiredService<IControlPlaneProbe>(), new Output(_out, _error, SliceHarness.Json), scope);
}
