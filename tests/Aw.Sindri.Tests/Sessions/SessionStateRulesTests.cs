using Aw.Sindri.Core.Sessions;
using static Aw.Sindri.Tests.Sessions.SessionData;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>The state merge (collector spec §5.2, §5.3).</summary>
public sealed class SessionStateRulesTests
{
    private static MergedState Merge(ClaudeRecord? record = null, FleetStateFile? hook = null, DateTimeOffset? transcript = null, DateTimeOffset? now = null) =>
        SessionStateRules.Merge(new StateInputs(record, hook, transcript, now ?? T0));

    [Fact, Trait("Lane", "unit")]
    public void Merge_keeps_asking_until_a_later_hook_event()
    {
        var asking = Hook(HookState.Asking, T0.AddSeconds(10), AskingReason.Approval, tool: "Bash");

        var held = Merge(Record("busy", T0.AddSeconds(5)), asking, now: T0.AddSeconds(30));
        var released = Merge(Record("busy", T0.AddSeconds(5)), Hook(HookState.Running, T0.AddSeconds(20)), now: T0.AddSeconds(30));

        Assert.Equal((SessionState.Asking, AskingReason.Approval, "Bash", StateSource.Hook), (held.State, held.Reason, held.Tool, held.Source));
        Assert.Equal((SessionState.Running, StateSource.Hook), (released.State, released.Source));
    }

    [Fact, Trait("Lane", "unit")]
    public void Merge_newer_record_status_wins()
    {
        var hook = Hook(HookState.Asking, T0.AddSeconds(10), AskingReason.Approval, tool: "Bash");

        var newerRecord = Merge(Record("busy", T0.AddSeconds(11)), hook, now: T0.AddSeconds(12));
        var olderRecord = Merge(Record("idle", T0.AddSeconds(9)), hook, now: T0.AddSeconds(12));

        Assert.Equal((SessionState.Running, StateSource.Record, T0.AddSeconds(11)), (newerRecord.State, newerRecord.Source, newerRecord.Since));
        Assert.Equal(SessionState.Asking, olderRecord.State);
        Assert.Equal(SessionState.Idle, Merge(Record("idle", T0), now: T0.AddSeconds(1)).State);
    }

    [Fact, Trait("Lane", "unit")]
    public void Merge_transcript_fallback_waits_10s()
    {
        var hook = Hook(HookState.Asking, T0, AskingReason.Approval, tool: "Bash");

        var early = Merge(hook: hook, transcript: T0.AddSeconds(10), now: T0.AddSeconds(11));
        var late = Merge(hook: hook, transcript: T0.AddSeconds(11), now: T0.AddSeconds(12));

        Assert.Equal(SessionState.Asking, early.State);
        Assert.Equal((SessionState.Running, StateSource.Transcript, true), (late.State, late.Source, late.Estimated));
    }

    [Fact, Trait("Lane", "unit")]
    public void Merge_estimates_from_the_transcript_without_a_hook_or_a_usable_status()
    {
        var recent = Merge(Record("shell", T0), transcript: T0.AddSeconds(-30));
        var old = Merge(transcript: T0.AddSeconds(-61));
        var none = Merge();

        Assert.Equal((SessionState.Running, StateSource.Transcript, true), (recent.State, recent.Source, recent.Estimated));
        Assert.Equal((SessionState.Idle, true), (old.State, old.Estimated));
        Assert.Equal((SessionState.Unknown, StateSource.None, false), (none.State, none.Source, none.Estimated));
    }

    [Fact, Trait("Lane", "unit")]
    public void Merge_reads_an_ended_hook_file_of_a_live_process_as_unknown()
    {
        var merged = Merge(hook: Hook(HookState.Ended, T0), now: T0.AddSeconds(5));

        Assert.Equal((SessionState.Unknown, StateSource.Hook), (merged.State, merged.Source));
    }
}
