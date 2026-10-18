using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.ListSessions;
using Aw.Sindri.Tests.Sessions;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The session routes' goldens (collector spec CA-7): the snapshot is published straight into the host's in-memory store, as the collector's pass would, so the bodies depend on nothing
/// the box runs. The Claude row is in tmux under Warp, the Codex row asks for approval, the Cursor row has an unreported id and a launch.
/// </summary>
internal static class SessionScenarios
{
    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("sessions", "empty"), ("sessions", "fixture-three-providers"), ("sessions", "session-scoped"), ("sessions", "problems-source-failed"),
        ("sessions", "recently-ended"), ("sessions", "unsupported-platform"), ("sessions", "proc-unreadable"), ("sessions", "disabled"),
        ("sessions-id", "claude-tmux"), ("sessions-id", "codex-hook"), ("sessions-id", "not-found"), ("sessions-id", "session-caller-hidden-fields"),
        ("status", "sessions"),
    ];

    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var (sessions, ended, problems) = Snapshot(scenario);
        host.App.Services.GetRequiredService<ISessionSnapshot>().Publish(new SessionSnapshot(sessions, ended, problems, SessionData.T0.AddMinutes(5)));
        var human = Human(host.Token.Value);
        (string, string)[] session = [(SindriHeaders.Caller, "session"), (SindriHeaders.Repo, "api")];
        var (path, headers) = (slug, scenario) switch
        {
            ("sessions-id", "claude-tmux") => ($"/sindri/sessions/claude-code/{SessionsRig.ClaudeId}", human),
            ("sessions-id", "codex-hook") => ($"/sindri/sessions/codex/{SessionsRig.CodexId}", human),
            ("sessions-id", "not-found") => ("/sindri/sessions/codex/not-here", human),
            ("sessions-id", _) => ($"/sindri/sessions/claude-code/{SessionsRig.ClaudeId}", session),
            ("status", _) => ("/sindri/status", human),
            (_, "session-scoped") => ("/sindri/sessions", session),
            _ => ("/sindri/sessions", human),
        };
        return (await host.Http.Ask(Get(path, [.. headers]))).Body;
    }

    private static (IReadOnlyList<ObservedSession>, IReadOnlyList<EndedSession>, IReadOnlyList<SessionProblem>) Snapshot(string scenario) => scenario switch
    {
        "empty" => ([], [], []),
        "problems-source-failed" or "sessions" => (SessionsRig.Three, [], [new SessionProblem("claude-records", "SND-SESSION-001", "The claude-records source couldn't be read in the last pass")]),
        "recently-ended" => ([SessionsRig.Codex()], [new EndedSession(SessionsRig.Claude(), SessionData.T0.AddMinutes(4))], []),
        "unsupported-platform" or "proc-unreadable" or "disabled" =>
            (SessionsRig.Three, [], [new SessionProblem("processes", "SND-SESSION-002", $"Sindri can't see agent sessions on this system: {scenario}")]),
        _ => (SessionsRig.Three, [], []),
    };
}
