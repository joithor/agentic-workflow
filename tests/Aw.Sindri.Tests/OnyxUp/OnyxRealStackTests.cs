using Aw.Kernel.Json;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Registration;
using Aw.Sindri.Features;
using Aw.Sindri.Features.OnyxDown;
using Aw.Sindri.Features.OnyxStatus;
using Aw.Sindri.Features.OnyxUp;
using Aw.Sindri.Features.RunDoctor;
using Aw.Sindri.Host;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Registration;
using Aw.Sindri.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.OnyxUp;

/// <summary>
/// The real thing, once: `onyx up` on this box's Docker pulls the pinned images and starts the stack, `/api/version` answers v4.9.0, nginx is published on 127.0.0.1 only, a
/// second `up` does nothing, `down` stops it. Heavy: it pulls about 2.7 GB of images and starts Onyx, so it skips when Docker is missing, when the disk is short, and it is not run by
/// the tasks that wrote it (the orchestrator runs it, under a heavy-job slot). Its cleanup removes the stack and its volumes; that is the test's own, not a product behavior.
/// </summary>
public sealed class OnyxRealStackTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "heavy")]
    public async Task Up_down_cycle_on_the_real_docker()
    {
        var home = Path.Combine(Path.GetTempPath(), "aw-onyx-real-" + Guid.NewGuid().ToString("N"));
        string? Environment(string name) => string.Equals(name, "AW_SINDRI_HOME", StringComparison.Ordinal) || string.Equals(name, "AW_STATE_DIR", StringComparison.Ordinal) ? home : null;
        await using var provider = new ServiceCollection()
            .AddSingleton(TimeProvider.System)
            .AddSingleton(AwJson.CreateOptions(SindriJsonContext.Default))
            .AddSingleton<IOnyxProgress>(new CapturingOnyxProgress())
            .AddSindriCore()
            .AddSindriFeatures()
            .AddSindriInfrastructure(Environment)
            .BuildServiceProvider();
        var docker = provider.GetRequiredService<IContainerRuntime>().Inspect(Token);
        Assert.SkipWhen(!docker.CliFound || !docker.DaemonAnswers, "Docker isn't available on this box");
        Assert.SkipWhen(provider.GetRequiredService<IHostProbes>().FreeDisk(Path.GetTempPath()).FreeBytes < provider.GetRequiredService<IOnyxPinsSource>().Pins.DiskBytes, "not enough free disk for the Onyx images");
        var paths = OnyxPaths.For(home);
        string? created = null;
        try
        {
            await Cycle(provider, paths, name => created = name);
        }
        finally
        {
            // Only the project this test's own home started is removed: its name is per home (never the owner's), and it is the one state.json recorded after `up`.
            if (created is not null && string.Equals(created, paths.Project.Name, StringComparison.Ordinal) && ComposeProject.IsOurs(created))
            {
                _ = new ProcessToolRunner().Run(
                    "docker",
                    ["compose", "-p", created, "-f", paths.Compose, "-f", paths.Override, "--env-file", paths.Env, "down", "-v"],
                    TimeSpan.FromMinutes(5),
                    cancellationToken: CancellationToken.None);
            }

            if (Directory.Exists(home))
            {
                Directory.Delete(home, recursive: true);
            }
        }
    }

    private static async Task Cycle(ServiceProvider provider, OnyxPaths paths, Action<string> started)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var runtime = services.GetRequiredService<IContainerRuntime>();
        var up = (await services.GetRequiredService<IService<OnyxUpRequest, OnyxUpResponse>>().Handle(new OnyxUpRequest(), Token)).Value;
        var recorded = services.GetRequiredService<IOnyxWorkspace>().ReadState()?.Project;
        Assert.Equal(paths.Project.Name, recorded);   // the project this home started: per home, never a shared name
        started(paths.Project.Name);
        using var http = new HttpClient();

        Assert.Contains("v4.9.0", await http.GetStringAsync(new Uri(up.Url + OnyxRoutes.Version), Token), StringComparison.Ordinal);
        Assert.All(runtime.Ps(paths.Project, Token).Value.SelectMany(static c => c.Publishers), static p => Assert.Equal("127.0.0.1", p.Address));
        Assert.Equal(CheckState.Ok, (await services.GetServices<IDoctorChecks>().OfType<OnyxStackDoctor>().Single().Check(Token))!.State);
        var again = (await services.GetRequiredService<IService<OnyxUpRequest, OnyxUpResponse>>().Handle(new OnyxUpRequest(), Token)).Value;
        Assert.Equal(["fetch", "config", "pull", "start"], again.Skipped, StringComparer.Ordinal);

        _ = (await services.GetRequiredService<IService<OnyxDownRequest, OnyxDownResponse>>().Handle(new OnyxDownRequest(), Token)).Value;
        Assert.DoesNotContain(runtime.Ps(paths.Project, Token).Value, static c => c.State == "running");
        var status = (await services.GetRequiredService<IService<OnyxStatusRequest, OnyxStatusResponse>>().Handle(new OnyxStatusRequest(), Token)).Value;
        Assert.Equal(OnyxState.Stopped, status.State);
    }
}
