using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Launch;

/// <summary>
/// ILauncher is idempotent per LaunchId across a restart (engine P8 re-sends the same id after a Sindri restart): a second launcher over the same
/// state directory and a real tmux server answers the live session and never stops it.
/// </summary>
[Lane(TestLane.Integration)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class LauncherRestartTests : IDisposable
{
    private readonly PrivateTmux _tmux = new();
    private readonly string _home = Directory.CreateTempSubdirectory("aw-launcher-restart-").FullName;
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly string _wrap;

    public LauncherRestartTests()
    {
        _wrap = Path.Combine(_tmux.Scratch, "wrap.sh");   // stands in for 'sindri session wrap': it just stays alive; written before any process of this class starts, so no fork of ours holds it open
        _ = ExecutableScript.Write(_wrap, "sleep 60\n");
    }

    public void Dispose()
    {
        _tmux.Dispose();
        Directory.Delete(_home, recursive: true);
    }

    private Launcher Make(string wrap) =>
        new(new LauncherParts(new AgentCatalog([AgentEntries.ClaudeCode]), _ => new TmuxSessionHost(_tmux.Options), new FakeTerminalViewer(), new LauncherOptions(_home) { SindriExecutable = wrap }, _clock)
        {
            ResolveBin = static (_, _) => ValueTask.FromResult<string?>(null),
        });

    [Fact]
    public async Task The_same_launch_id_after_a_restart_answers_the_live_session()
    {
        var wrap = _wrap;
        var request = A.LaunchRequest().Build();
        var first = await Make(wrap).Launch(request, TestContext.Current.CancellationToken);
        Expect.True(first is StartedLaunch);
        var name = ((StartedLaunch)first).Host.Name;
        var hashFile = LaunchSecrets.PathFor(new LauncherOptions(_home).SecretsDirectory, request.Launch);
        var hash = await File.ReadAllTextAsync(hashFile, TestContext.Current.CancellationToken);

        var again = await Make(wrap).Launch(request, TestContext.Current.CancellationToken);

        Expect.True(again is StartedLaunch started && started.Host.Name == name);
        Expect.True(_tmux.HasSession(name));
        Expect.Equal(hash, await File.ReadAllTextAsync(hashFile, TestContext.Current.CancellationToken));
    }
}
