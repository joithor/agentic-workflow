namespace Aw.Sindri.Core.Sessions;

/// <summary>Why the session collector can't run (SND-SESSION-002, collector spec CA-5).</summary>
public enum UnavailableReason
{
    /// <summary>There is no /proc here (macOS, Windows).</summary>
    UnsupportedPlatform,

    /// <summary>/proc exists and couldn't be read.</summary>
    ProcUnreadable,

    /// <summary>launcher.json turned the collector off.</summary>
    Disabled,
}
