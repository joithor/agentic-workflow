using Aw.Kernel.Telemetry;
using Aw.Kernel.Testing;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Registration;
using Aw.Sindri.Host.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Launch.Viewers;

/// <summary>The Warp launch files left by an earlier run are swept when the launcher service starts (UI spec 6.3), not only when a Warp viewer is next built.</summary>
[Lane(TestLane.Unit)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class WarpStartupSweepTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aw-warp-sweep-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task Starting_the_launcher_service_sweeps_stale_warp_launch_files_and_keeps_others()
    {
        var launchDir = Path.Combine(_dir, "warp");
        _ = Directory.CreateDirectory(launchDir);
        var stale = Path.Combine(launchDir, "sindri-snd-old.yaml");
        var mine = Path.Combine(launchDir, "mine.yaml");
        foreach (var file in new[] { stale, mine })
        {
            await File.WriteAllTextAsync(file, "name: x\n", TestContext.Current.CancellationToken);
            File.SetLastWriteTimeUtc(file, new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        }

        var services = Composition.Compose(
            new ServiceCollection(),
            name => name switch { "AW_STATE_DIR" => Path.Combine(_dir, "state"), "AW_WARP_LAUNCH_DIR" => launchDir, _ => null },
            new FakeTimeProvider());
        await using var provider = services.AddSindriServe().AddOptions().BuildServiceProvider();
        _ = provider.GetRequiredService<TelemetryRuntime>();
        var host = provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>().OfType<LauncherHost>().Single();

        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        Assert.False(File.Exists(stale));
        Assert.True(File.Exists(mine));
    }
}
