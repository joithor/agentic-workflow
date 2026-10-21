using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Testing.Launch;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Launch.Viewers;

/// <summary>A scratch directory, stub scripts (no display is ever needed), a fake host holding one session, and a fake clock.</summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
internal sealed class ViewerFixture : IDisposable
{
    public string Dir { get; } = Directory.CreateTempSubdirectory("aw-viewer-").FullName;

    public FakeTimeProvider Clock { get; } = new(KnownInstants.Epoch);

    public FakeSessionHost Host { get; } = new();

    public HostRef Session { get; private set; } = new(HostKind.Tmux, "snd-one");

    public async Task StartSession()
    {
        var started = await Host.Start(new HostSession(Session.Name, Dir, ["sleep", "1"], new Dictionary<string, string>(StringComparer.Ordinal)), TimeSpan.FromSeconds(4), CancellationToken.None);
        Session = started.Value;
    }

    public ViewerRequest Request(string title = "sindri ITM-1") =>
        new(Session, Host.AttachArgv(Session), title, TimeSpan.FromSeconds(3)) { Cwd = Dir };

    /// <summary>Writes an executable shell script and returns its path.</summary>
    public string Script(string name, string body)
    {
        var path = Path.Combine(Dir, name);
        return ExecutableScript.Write(path, body + "\n");
    }

    /// <summary>Awaits the task while moving the fake clock 250 ms at a time (the observation window's poll), with real pauses so processes can run. While <paramref name="hold"/> says true the clock does not move.</summary>
    public async Task<T> Drive<T>(ValueTask<T> pending, Action<int>? onTick = null, Func<bool>? hold = null)
    {
        var task = pending.AsTask();
        var held = 0;
        for (var tick = 0; !task.IsCompleted && tick < 400; tick++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
            if (hold?.Invoke() == true && held++ < 3000)   // the clock stands still, so the window cannot end before the stub has finished (30 s at most)
            {
                tick--;
                continue;
            }

            onTick?.Invoke(tick);

            Clock.Advance(TimeSpan.FromMilliseconds(250));
        }

        return await task.WaitAsync(TimeSpan.FromSeconds(30), TimeProvider.System, TestContext.Current.CancellationToken);   // a hang fails the test, never holds the slot
    }

    public void Dispose() => Directory.Delete(Dir, recursive: true);
}
