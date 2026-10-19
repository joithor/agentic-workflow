using System.Runtime.Versioning;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Features.GetSessionPreview;
using Aw.Sindri.Infrastructure.Sessions;
using Aw.Sindri.Tests.ListSessions;
using Aw.Sindri.Tests.Sessions;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.GetSessionPreview;

/// <summary>
/// The preview end to end on a private tmux server (tmux -L aw-co2b-&lt;pid&gt;-preview, never the default one or a team's): a pane that prints known lines with
/// escape sequences and a long line, read through the service with the real tmux adapter and the real process table.
/// </summary>
[UnsupportedOSPlatform("windows")]
public sealed class PreviewTmuxIntegrationTests : IDisposable
{
    private readonly SessionsRig _r = new();
    private readonly string _directory = Directory.CreateTempSubdirectory("aw-preview-pane").FullName;
    private PrivateTmux? _server;

    [Fact, Trait("Lane", "integration")]
    public async Task Preview_reads_the_stub_pane_through_tmux()
    {
        var server = Server();
        var script = Path.Combine(_directory, "pane.sh");
        await File.WriteAllTextAsync(script, string.Join('\n',
            "printf 'plain line\\n'",
            "printf '\\033[31mred\\033[0m text\\033]0;title\\007 after\\n'",
            "head -c 300 /dev/zero | tr '\\0' 'x'; printf '\\n'",
            "printf 'last line\\n'",
            "exec sleep 300") + "\n", SessionsRig.Token);
        var pane = server.NewPane($"sh {script}");
        Assert.SkipWhen(pane is null, "tmux refused a new window");
        var (paneId, panePid) = pane.Value;
        var table = new ProcProcessTable("/proc", OwnUid.Read());
        var cli = new TmuxCli(new FakeTimeProvider(SessionData.T0), "tmux", TimeSpan.FromSeconds(5));
        var service = new GetSessionPreviewService(_r.Snapshot, _r.Base.Callers, new PaneCaptures(cli), table, _r.Base.Clock);
        var self = table.Read(SessionsRig.Token).Value.First(p => p.Pid == panePid);
        Publish(panePid, self.StartTicks, server.Socket, paneId);
        using var human = _r.Human();

        var preview = await ReadWhenPrinted(service);
        var plain = SessionsRig.Codex();
        _ = _r.Publish([plain, plain with { Ref = plain.Ref with { Id = SessionId.From("other") }, Pid = Environment.ProcessId }]);
        var notInTmux = await service.Handle(new GetSessionPreviewRequest(plain.Ref), SessionsRig.Token);
        var elsewhere = SessionsRig.Claude();
        _ = _r.Publish([elsewhere with { Pid = Environment.ProcessId, StartTicks = table.Read(SessionsRig.Token).Value.First(p => p.Pid == Environment.ProcessId).StartTicks, Host = elsewhere.Host with { Tmux = new TmuxFacts(server.Socket, "other", "co2b", paneId) } }]);
        var notLive = await service.Handle(new GetSessionPreviewRequest(elsewhere.Ref), SessionsRig.Token);   // this test process is not below the pane's shell
        _ = _r.Publish([elsewhere with { Host = elsewhere.Host with { Tmux = new TmuxFacts(server.Socket, "other", "co2b", "%987654") } }]);
        var gone = await service.Handle(new GetSessionPreviewRequest(elsewhere.Ref), SessionsRig.Token);

        Assert.Equal(["plain line", "red text after", new string('x', PreviewRules.MaxLineLength), "last line"], preview.Lines);
        Assert.True(preview.Truncated);   // the 300-character line was cut at 200
        Assert.Equal(PreviewReason.NotInTmux, Assert.IsType<SessionPreviewUnavailable>(notInTmux.Value).Reason);
        Assert.Equal(PreviewReason.NotLive, Assert.IsType<SessionPreviewUnavailable>(notLive.Value).Reason);
        Assert.Equal(PreviewReason.NotLive, Assert.IsType<SessionPreviewUnavailable>(gone.Value).Reason);
        TestContext.Current.SendDiagnosticMessage(
            $"PROOF lines={preview.Lines.Count} truncated={preview.Truncated} lengths=[{string.Join(',', preview.Lines.Select(static l => l.Length))}] first='{preview.Lines[0]}' second='{preview.Lines[1]}' "
            + $"notInTmux={Assert.IsType<SessionPreviewUnavailable>(notInTmux.Value).Reason} notLive={Assert.IsType<SessionPreviewUnavailable>(notLive.Value).Reason} gone={Assert.IsType<SessionPreviewUnavailable>(gone.Value).Reason}");
    }

    private void Publish(int pid, long ticks, string socket, string pane)
    {
        var session = SessionsRig.Claude();
        _ = _r.Publish([session with { Pid = pid, StartTicks = ticks, Host = session.Host with { Tmux = new TmuxFacts(socket, "other", "co2b", pane) } }]);
    }

    /// <summary>The pane's shell may not have printed yet: read until the last line is there, with a bound.</summary>
    private static async Task<SessionPreview> ReadWhenPrinted(GetSessionPreviewService service)
    {
        SessionPreview? last = null;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            last = (await service.Handle(new GetSessionPreviewRequest(SessionsRig.Claude().Ref), SessionsRig.Token)).Value as SessionPreview;
            if (last?.Lines.Contains("last line", StringComparer.Ordinal) == true)
            {
                return last;
            }

            await Task.Delay(100, SessionsRig.Token);
        }

        Assert.Fail("the pane never printed its last line: " + string.Join('|', last?.Lines ?? []));
        return last;
    }

    private PrivateTmux Server()
    {
        _server ??= PrivateTmux.Start("preview");
        Assert.SkipWhen(_server is null, "tmux isn't installed or its private server didn't start");
        return _server;
    }

    public void Dispose()
    {
        _server?.Dispose();
        Directory.Delete(_directory, recursive: true);
    }
}
