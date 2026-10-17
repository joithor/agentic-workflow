namespace Aw.Sindri.Core.Sessions;

/// <summary>The state the aw:fleet hook wrote (collector spec §5.1).</summary>
public enum HookState
{
    /// <summary>A turn is running.</summary>
    Running,

    /// <summary>The agent waits on the human.</summary>
    Asking,

    /// <summary>The turn ended.</summary>
    Idle,

    /// <summary>The session ended.</summary>
    Ended,
}
