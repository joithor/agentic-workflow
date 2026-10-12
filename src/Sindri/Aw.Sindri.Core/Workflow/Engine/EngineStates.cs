namespace Aw.Sindri.Core.Workflow.Engine;

/// <summary>The engine-owned states (spec §3.8), reserved in recipes (RCP-SCHEMA rules): status shows one while a suspension is over the recipe frame, or once the instance was cancelled.</summary>
public static class EngineStates
{
    /// <summary>A park: the recipe frame waits for an answer (or a re-approval) to resume.</summary>
    public const string Parked = "parked";

    /// <summary>An escalation: a human decides.</summary>
    public const string Escalated = "escalated";

    /// <summary>A takeover: the human works the item; the interpreter pauses the state's timer.</summary>
    public const string TakenOver = "taken-over";

    /// <summary>The engine's terminal state, with outcome 'cancelled' (W6).</summary>
    public const string Cancelled = "cancelled";

    /// <summary>Whether <paramref name="state"/> is engine-owned.</summary>
    /// <param name="state">A state name.</param>
    /// <returns>True for parked, escalated, taken-over and cancelled.</returns>
    public static bool IsEngineOwned(string state) => string.Equals(state, Parked, StringComparison.Ordinal) || string.Equals(state, Escalated, StringComparison.Ordinal)
        || string.Equals(state, TakenOver, StringComparison.Ordinal) || string.Equals(state, Cancelled, StringComparison.Ordinal);
}
