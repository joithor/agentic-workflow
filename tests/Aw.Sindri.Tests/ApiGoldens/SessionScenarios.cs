using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Core.Workflow.Engine;
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
        ("sessions-id", "claude-tmux"), ("sessions-id", "codex-hook"), ("sessions-id", "not-found"), ("sessions-id", "session-caller-hidden-fields"), ("sessions-id", "cursor-engine-launch"),
        ("status", "sessions"),
    ];

    /// <summary>The golden host's profile: the repos the fixture sessions work in, so a session's worktree is served relative to its checkout.</summary>
    public const string ProfileYaml = "schemaVersion: 1\nuser: golden\nrepos:\n  - { name: api, path: /work/api }\n  - { name: web, path: /work/web }\nservices:\n  heartbeat: { every: 5s }\n";

    private static readonly string[] _engineLaunch = ["fixture-three-providers", "cursor-engine-launch"];

    private static readonly Dictionary<string, UnavailableReason> _reasons = new(StringComparer.Ordinal)
    {
        ["unsupported-platform"] = UnavailableReason.UnsupportedPlatform,
        ["proc-unreadable"] = UnavailableReason.ProcUnreadable,
        ["disabled"] = UnavailableReason.Disabled,
    };

    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var (sessions, ended, problems) = Snapshot(scenario);
        host.App.Services.GetRequiredService<ISessionSnapshot>().Publish(new SessionSnapshot(sessions, ended, problems, SessionData.T0.AddMinutes(5)) { History = History() });
        var human = Human(host.Token.Value);
        var hash = (await host.LiveHash(ct)).Value;   // the service reads repo paths from the approved profile, so approve it first
        var approved = await host.Http.Ask(Post("/sindri/profile/approve", await ApiScenarios.Confirmed(host, hash), human));
        Assert.True(approved.Status == System.Net.HttpStatusCode.OK, approved.Body);
        if (_engineLaunch.Contains(scenario, StringComparer.Ordinal))
        {
            // The Cursor row's launch is the engine's: its instance, Step and attempt come from the 'launches' view, folded from the ledger.
            var item = "ITM-00000000000000000000000000000001";
            await LaunchScenarios.Started(host, LaunchId.From(SessionsRig.Launch), item, ViewerOutcomeKind.Skipped, ct, LaunchScenarios.ViewerOf("none", "none", ViewerOutcomeKind.Skipped),
                new LaunchStep(InstanceAddress.From(item + "/step/1"), "build", 2), agent: "cursor");
            await ViewScenarios.CatchUp(host, ct);
        }

        (string, string)[] session = [(SindriHeaders.Caller, "session"), (SindriHeaders.Repo, "api")];
        var (path, headers) = (slug, scenario) switch
        {
            ("sessions-id", "claude-tmux") => ($"/sindri/sessions/claude-code/{SessionsRig.ClaudeId}", human),
            ("sessions-id", "codex-hook") => ($"/sindri/sessions/codex/{SessionsRig.CodexId}", human),
            ("sessions-id", "cursor-engine-launch") => ("/sindri/sessions/cursor/pid-333-100", human),
            ("sessions-id", "not-found") => ("/sindri/sessions/codex/not-here", human),
            ("sessions-id", _) => ($"/sindri/sessions/claude-code/{SessionsRig.ClaudeId}", session),
            ("status", _) => ("/sindri/status", human),
            (_, "session-scoped") => ("/sindri/sessions", session),
            _ => ("/sindri/sessions", human),
        };
        return (await host.Http.Ask(Get(path, [.. headers]))).Body;
    }

    // Claude went idle, then asked for approval, then was approved: pins the entry shape and its oldest-first order for U11a's sheet.
    private static Dictionary<SessionRef, IReadOnlyList<SessionStateEntry>> History() => new()
    {
        [SessionsRig.Claude().Ref] =
        [
            new SessionStateEntry(SessionState.Idle, Reason: null, SessionData.T0.AddMinutes(-2), StateSource.Hook),
            new SessionStateEntry(SessionState.Asking, AskingReason.Approval, SessionData.T0.AddMinutes(-1), StateSource.Hook),
            new SessionStateEntry(SessionState.Running, Reason: null, SessionData.T0, StateSource.Record),
        ],
    };

    private static (IReadOnlyList<ObservedSession>, IReadOnlyList<EndedSession>, IReadOnlyList<SessionProblem>) Snapshot(string scenario) => scenario switch
    {
        "empty" => ([], [], []),
        "problems-source-failed" or "sessions" => (SessionsRig.Three, [], [new SessionProblem("claude-records", "SND-SESSION-001", "The claude-records source couldn't be read in the last pass")]),
        "recently-ended" => ([SessionsRig.Codex()], [new EndedSession(SessionsRig.Claude(), SessionData.T0.AddMinutes(4))], []),
        "unsupported-platform" or "proc-unreadable" or "disabled" =>
            (SessionsRig.Three, [], [new SessionProblem("processes", "SND-SESSION-002", $"Sindri can't see agent sessions on this system: {scenario}") { Reason = _reasons[scenario] }]),
        _ => (SessionsRig.Three, [], []),
    };
}
