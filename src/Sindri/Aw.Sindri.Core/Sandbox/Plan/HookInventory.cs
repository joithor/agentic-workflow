namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>The closed inventory of the hooks that run in a session and the state paths they need (sandbox spec §3.1).</summary>
public static class HookInventory
{
    /// <summary>The state-directory exceptions, taken from the installed hooks (config/hooks, lib/failopen.sh, the judge wrapper and bin-r1b/heavy).</summary>
    public static IReadOnlyList<StatePathEntry> StatePaths { get; } =
    [
        new("judge", StatePathKind.ReadWrite, "done-gate, external-write-guard, turn-origin, send-gate, outbox-flush, record-teammate-name, subagent-start-map, judge: decisions.sqlite with its -wal and -shm, sessions, outbox, teammate names"),
        new("judge/config.json", StatePathKind.ReadOnly, "judge: the configuration stays the owner's"),
        new("logs", StatePathKind.PerLaunchDir, "lib/failopen.sh appends failopen.log; the launcher reads the per-launch log into the run's denial summary"),
        new("probe", StatePathKind.PerLaunchDir, "probe-log writes the hook-input probe"),
        new("digests", StatePathKind.PerLaunchDir, "context-guard writes digests; a Step can't read other sessions' excerpts"),
        new("context-guard", StatePathKind.ReadOnly, "context-guard reads its configuration"),
        new("context-guard/state", StatePathKind.ReadWrite, "context-guard writes per-session state"),
        new("locks", StatePathKind.ReadWrite, "heavy: mkdir mutexes, heavy-job.lock and heavy-session"),
        new("heavy-slots-cap", StatePathKind.ReadOnly, "heavy reads the box-wide cap"),
        new("heavy-meta-reserve", StatePathKind.ReadOnly, "heavy reads the meta reserve"),
        new("bin-r1b", StatePathKind.ReadOnly, "the heavy wrapper"),
        new("meta", StatePathKind.Absent, "meta-outbox, meta-stop-gate and meta-lane-gate do nothing without it, by design"),
        new("bin/aw-meta", StatePathKind.Absent, "meta hooks exit at once when aw-meta is not executable"),
    ];

    /// <summary>The installed hook scripts and whether each answers permissions.</summary>
    public static IReadOnlyList<HookScript> Scripts { get; } =
    [
        Plain("block-destructive"),
        Plain("block-push-main"),
        Plain("context-guard"),
        Plain("detect-secrets"),
        Plain("done-gate"),
        Plain("done-gate-annotate"),
        Answers("external-write-guard"),
        Plain("git-context"),
        Plain("judge-health"),
        Plain("meta-lane-gate"),
        Plain("meta-outbox"),
        Plain("meta-stop-gate"),
        Plain("outbox-flush"),
        Plain("prism-context"),
        Plain("probe-log"),
        Plain("prompt-sort"),
        Answers("record-teammate-name"),
        Answers("rtk-rewrite"),
        Answers("scope-gate"),
        Answers("send-gate"),
        Plain("session-id-report"),
        Plain("sindri-nudge"),
        Plain("subagent-start-map"),
        Plain("turn-origin"),
    ];

    private static HookScript Plain(string name) => new(name, AnswersPermissions: false);

    private static HookScript Answers(string name) => new(name, AnswersPermissions: true);

    /// <summary>The hooks a sandboxed session keeps and omits, by <see cref="HookPermissionPolicy.OmitPermissionAnsweringHooks"/>.</summary>
    /// <returns>The two lists.</returns>
    public static PlanHooks ForSession()
    {
        var omit = HookPermissionPolicy.OmitPermissionAnsweringHooks;
        return new PlanHooks(
            [.. Scripts.Where(s => !(omit && s.AnswersPermissions)).Select(s => s.Name)],
            [.. Scripts.Where(s => omit && s.AnswersPermissions).Select(s => s.Name)]);
    }
}
