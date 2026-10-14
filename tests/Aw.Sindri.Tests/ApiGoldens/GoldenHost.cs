using Aw.Kernel.Ids;
using Aw.Kernel.Services;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.StartProcess;
using Aw.Sindri.Host;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// A control plane whose answers are byte-stable, for the API goldens (UI spec E-17, plan P1 unit E): real adapters in a
/// temp home with a fixed profile, a <see cref="FakeTimeProvider"/> at <see cref="KnownInstants.Epoch"/>, sequential ids,
/// a fixed pid, the lease held (StartProcess under service:host) and a fixed human token held, on loopback through the
/// pipeline Program runs (the wiring of CliEndToEndTests). Each scenario starts its own, so no golden depends on the
/// order the scenarios run in.
/// </summary>
public sealed class GoldenHost : IAsyncDisposable
{
    /// <summary>The pid the fake process probe reports for this process.</summary>
    public const int Pid = 4242;

    /// <summary>The profile every golden host starts with: one scheduled service, so status lists it.</summary>
    public const string ProfileYaml = "schemaVersion: 1\nuser: golden\nservices:\n  heartbeat: { every: 5s }\n";

    private readonly TempHome _home = new();
    private readonly TempHome? _elsewhere;
    private WebApplication? _app;

    private readonly string _profileYaml;
    private readonly Action<IServiceCollection>? _extra;

    private GoldenHost(bool temporary, string profileYaml, Action<IServiceCollection>? extra)
    {
        _elsewhere = temporary ? null : new TempHome();
        _profileYaml = profileYaml;
        _extra = extra;
    }

    /// <summary>The token the host holds.</summary>
    public HumanToken Token { get; } = new("golden-human-token");

    /// <summary>The temp home (normalized to '&lt;home&gt;' in the goldens).</summary>
    public string Home => _home.Path;

    /// <summary>A client on the host's loopback address.</summary>
    public HttpClient Http { get; private set; } = new();

    /// <summary>The running host.</summary>
    public WebApplication App => _app ?? throw new InvalidOperationException("the host isn't started");

    /// <summary>Starts a host. <paramref name="temporary"/> false points TMPDIR and AW_STATE_DIR away from its home, so the prover label is refused.</summary>
    /// <param name="temporary">Whether the home counts as temporary (honours the prover label).</param>
    /// <param name="ct">Cancellation.</param>
    /// <param name="profileYaml">The live profile it starts with (<see cref="ProfileYaml"/> unless a test needs another).</param>
    /// <param name="extra">More service replacements, after the fixed ones (a test's log sink).</param>
    /// <returns>The started host.</returns>
    public static async Task<GoldenHost> Start(bool temporary, CancellationToken ct, string profileYaml = ProfileYaml, Action<IServiceCollection>? extra = null)
    {
        var host = new GoldenHost(temporary, profileYaml, extra);
        try
        {
            await host.Run(ct);
            return host;
        }
        catch
        {
            await host.DisposeAsync();
            throw;
        }
    }

    /// <summary>The live profile's hash.</summary>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The hash.</returns>
    public async Task<ProfileHash> LiveHash(CancellationToken ct) =>
        (await App.Services.GetRequiredService<IProfileSource>().Load(ct)).Value.Hash;

    public async ValueTask DisposeAsync()
    {
        Http.Dispose();
        if (_app is not null)
        {
            await _app.StopAsync(CancellationToken.None);
            await _app.DisposeAsync();
        }

        _home.Dispose();
        _elsewhere?.Dispose();
    }

    private async Task Run(CancellationToken ct)
    {
        _ = Directory.CreateDirectory(Path.Combine(_home.Path, "profile"));
        await File.WriteAllTextAsync(Path.Combine(_home.Path, "profile", "profile.yaml"), _profileYaml, ct);
        var app = LoopbackBinding.Build([], Environment, new FakeTimeProvider(KnownInstants.Epoch), services =>
        {
            _extra?.Invoke(services);
            _ = services.Replace(ServiceDescriptor.Singleton<IIdGenerator>(new SequentialIdGenerator()));
            _ = services.Replace(ServiceDescriptor.Singleton<IProcessProbe>(new FakeProcessProbe(Pid)));
            NoTemporal.Use(services);   // the Temporal side is faked: the goldens are about the API
        });
        _app = app;
        _ = Pipeline.Configure(app);
        using (app.Services.GetRequiredService<ICallerContextAccessor>().EnterService("host"))   // as serve starts it
        {
            await using var scope = app.Services.CreateAsyncScope();
            _ = (await scope.ServiceProvider.GetRequiredService<IService<StartProcessRequest, StartProcessResponse>>()
                .Handle(new StartProcessRequest(), ct)).Value;
        }

        app.Services.GetRequiredService<HeldHumanToken>().Hold(Token);
        await app.StartAsync(ct);
        Http = new HttpClient { BaseAddress = new Uri(app.Urls.First()), Timeout = TimeSpan.FromSeconds(10) };
    }

    private string? Environment(string name) => name switch
    {
        "AW_SINDRI_HOME" => _home.Path,
        "HOME" => Path.Combine(_home.Path, "provider-home"),            // doctor's provider files: never the real ones
        "AW_SINDRI_CLAUDE_BIN" => Path.Combine(_home.Path, "no-claude"),  // mcp register --apply: never the real claude
        "TMPDIR" when _elsewhere is not null => Path.Combine(_elsewhere.Path, "tmp"),
        "AW_STATE_DIR" when _elsewhere is not null => Path.Combine(_elsewhere.Path, "state"),
        _ => null,
    };
}
