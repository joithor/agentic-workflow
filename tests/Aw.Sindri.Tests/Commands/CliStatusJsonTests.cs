using Aw.Kernel.Services;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Features.StartProcess;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Commands;

/// <summary>'sindri status --json' against a real control plane on loopback: stdout is the API's body as served (C5).</summary>
public sealed class CliStatusJsonTests : IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task Cli_status_json_equals_the_raw_api_body()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = LoopbackBinding.Build([], n => n switch { "AW_SINDRI_HOME" => _home.Path, _ => null }, TimeProvider.System);
        _ = Pipeline.Configure(app);
        await using var scope = app.Services.CreateAsyncScope();
        var grant = (await scope.ServiceProvider.GetRequiredService<IService<StartProcessRequest, StartProcessResponse>>().Handle(new StartProcessRequest(), ct)).Value.Grant;
        await app.StartAsync(ct);
        var url = new Uri(app.Urls.First());
        _ = await app.Services.GetRequiredService<IEndpointFile>().Write(new ControlEndpoint(url, grant.Holder.Pid, grant.Epoch), ct);
        var probe = app.Services.GetRequiredService<IControlPlaneProbe>();
        using var client = new ControlPlaneClient(probe, SliceHarness.Json);
        var stdout = new StringWriter();

        var code = await new Verbs(client, probe, new Output(stdout, TextWriter.Null, SliceHarness.Json), scope.ServiceProvider).Status(json: true, ct);
        using var http = new HttpClient();
        var served = await http.GetStringAsync(new Uri(url, "/sindri/status"), ct);

        Assert.Equal((1, served + "\n"), (code, stdout.ToString()));   // exit 1: no profile is approved
        await app.StopAsync(ct);
    }
}
