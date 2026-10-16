namespace Aw.Sindri.Core.Sessions;

/// <summary>The state rules (collector spec §5.2, §5.3).</summary>
public static class SessionStateRules
{
    private static readonly TimeSpan _askingFallback = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _recent = TimeSpan.FromSeconds(60);

    /// <summary>The state from the newer of the record's status and the hook file, the transcript's age as the fallback.</summary>
    /// <param name="inputs">The sources.</param>
    /// <returns>The merged state.</returns>
    public static MergedState Merge(StateInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var record = FromRecord(inputs.Record);
        return (inputs.Hook, record) switch
        {
            (null, null) => Estimate(inputs),
            (null, { } r) => r,
            ({ } h, { } r) when r.Since > h.At => r,
            ({ } h, _) => FromHook(h, inputs),
        };
    }

    private static MergedState? FromRecord(ClaudeRecord? record)
    {
        var state = record?.Status switch
        {
            "busy" => SessionState.Running,
            "idle" => SessionState.Idle,
            _ => (SessionState?)null,
        };
        return state is { } s ? new MergedState(s, StateSource.Record, record?.StatusUpdatedAt ?? DateTimeOffset.MinValue) : null;
    }

    private static MergedState FromHook(FleetStateFile hook, StateInputs inputs) => hook.State switch
    {
        HookState.Running => new MergedState(SessionState.Running, StateSource.Hook, hook.At),
        HookState.Idle => new MergedState(SessionState.Idle, StateSource.Hook, hook.At),
        HookState.Ended => new MergedState(SessionState.Unknown, StateSource.Hook, hook.At),
        HookState.Asking => Asking(hook, inputs),
        _ => throw new ArgumentOutOfRangeException(nameof(hook), hook.State, "unknown hook state"),
    };

    private static MergedState Asking(FleetStateFile hook, StateInputs inputs) =>
        inputs.TranscriptWrite is { } write && write > hook.At + _askingFallback
            ? new MergedState(SessionState.Running, StateSource.Transcript, write) { Estimated = true }
            : new MergedState(SessionState.Asking, StateSource.Hook, hook.At) { Reason = hook.Reason ?? AskingReason.Approval, Tool = hook.Tool };

    private static MergedState Estimate(StateInputs inputs) => inputs.TranscriptWrite is { } write
        ? new MergedState(inputs.Now - write <= _recent ? SessionState.Running : SessionState.Idle, StateSource.Transcript, inputs.Now) { Estimated = true }
        : new MergedState(SessionState.Unknown, StateSource.None, inputs.Now);
}
