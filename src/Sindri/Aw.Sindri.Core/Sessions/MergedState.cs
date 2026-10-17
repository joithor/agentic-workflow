namespace Aw.Sindri.Core.Sessions;

/// <summary>The merged state of one session.</summary>
/// <param name="State">The state.</param>
/// <param name="Source">Where it came from.</param>
/// <param name="Since">When it last changed, as far as the source says.</param>
public sealed record MergedState(SessionState State, StateSource Source, DateTimeOffset Since)
{
    /// <summary>Why it is asking.</summary>
    public AskingReason? Reason { get; init; }

    /// <summary>The tool an approval is for.</summary>
    public string? Tool { get; init; }

    /// <summary>True when the state is a guess from the transcript's age.</summary>
    public bool Estimated { get; init; }
}
