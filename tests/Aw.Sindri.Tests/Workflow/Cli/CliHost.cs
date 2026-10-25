using Aw.Kernel.Services;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Features.StartProcess;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Workflow.Cli;

/// <summary>A control plane in a temp home for the CLI-over-HTTP tests (Temporal faked): the lease held, this start's token minted and held, its endpoint recorded.</summary>
internal static class CliHost
{
    /// <param name="home">The temp home the host lives in.</param>
    /// <param name="ct">Cancels the start.</param>
    /// <param name="beforePipeline">Adds middleware ahead of the pipeline (a test that makes the host slow to answer).</param>
    public static async Task<WebApplication> Started(TempHome home, CancellationToken ct, Action<WebApplication>? beforePipeline = null)
    {
        _ = Directory.CreateDirectory(Path.Combine(home.Path, "profile"));
        await File.WriteAllTextAsync(Path.Combine(home.Path, "profile", "profile.yaml"), "schemaVersion: 1\nuser: thor\n", ct);
        var app = LoopbackBinding.Build([], n => n switch { "AW_SINDRI_HOME" => home.Path, _ => null }, TimeProvider.System, NoTemporal.Use);   // the CLI verbs here don't use Temporal: a real dev server only slows the start under load
        beforePipeline?.Invoke(app);
        _ = Pipeline.Configure(app);
        Core.Lease.LeaseGrant grant;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            grant = (await scope.ServiceProvider.GetRequiredService<IService<StartProcessRequest, StartProcessResponse>>().Handle(new StartProcessRequest(), ct)).Value.Grant;
        }

        app.Services.GetRequiredService<HeldHumanToken>().Hold((await app.Services.GetRequiredService<IHumanTokenStore>().Mint(ct)).Value);
        await app.StartAsync(ct);
        _ = await app.Services.GetRequiredService<IEndpointFile>().Write(new ControlEndpoint(new Uri(app.Urls.First()), grant.Holder.Pid, grant.Epoch), ct);
        return app;
    }
}
