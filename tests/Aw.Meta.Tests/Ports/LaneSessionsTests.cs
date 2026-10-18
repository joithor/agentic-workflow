using Aw.Kernel.Testing;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ids;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Adapters;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Integration)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class LaneSessionsTests : IDisposable
{
    private readonly TempDirectory _dir = new();
    private readonly InMemoryLanePane _pane = new();
    private readonly InMemoryProcessTable _processes = new();

    [Fact]
    public async Task The_claude_under_the_panes_shell_reports_its_current_session_id()
    {
        _pane.Put("t1-api", "claude");
        _pane.SetPid("t1-api", 100);
        _processes.Add(new ProcessInfo(100, 1, "bash", 1, 1));
        _processes.Add(new ProcessInfo(101, 100, "claude --resume", 1, 1));
        await File.WriteAllTextAsync(Path.Combine(_dir.Path, "101.json"), "{\"pid\":101,\"sessionId\":\"s-now\",\"cwd\":\"/x\"}", TestContext.Current.CancellationToken);

        var found = await new PaneLaneSessions(_pane, _processes, _dir.Path).Current("t1-api", TestContext.Current.CancellationToken);

        Assert.Equal("s-now", found.Value!.Value.Value);
    }

    [Fact]
    public async Task No_pane_or_no_record_answers_null()
    {
        _pane.Put("t1-api", "claude");
        _pane.SetPid("t1-api", 100);
        _processes.Add(new ProcessInfo(100, 1, "bash", 1, 1));
        var sessions = new PaneLaneSessions(_pane, _processes, _dir.Path);

        Assert.Null((await sessions.Current("t1-api", TestContext.Current.CancellationToken)).Value);
        Assert.Null((await sessions.Current("t1-ui", TestContext.Current.CancellationToken)).Value);
    }

    [Fact]
    public async Task Recording_a_session_writes_the_id_owner_only_and_replaces_the_old_one()
    {
        var files = new DirectoryLaneFiles(_dir.Path);
        await File.WriteAllTextAsync(Path.Combine(_dir.Path, "api"), "s-old\n", TestContext.Current.CancellationToken);

        var recorded = await files.RecordSession("api", SessionId.From("s-new"), A.T0, TestContext.Current.CancellationToken);

        Assert.True(recorded.IsSuccess);
        Assert.Equal("s-new\n", await File.ReadAllTextAsync(Path.Combine(_dir.Path, "api"), TestContext.Current.CancellationToken));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Path.Combine(_dir.Path, "api")));
        Assert.True((await files.RecordSession("../x", SessionId.From("s"), A.T0, TestContext.Current.CancellationToken)).IsError);
    }

    public void Dispose() => _dir.Dispose();
}
