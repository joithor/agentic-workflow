namespace Aw.Sindri.Core.Sessions;

/// <summary>Where a session's state came from (collector spec §2).</summary>
public enum StateSource
{
    /// <summary>No source: the state is unknown.</summary>
    None,

    /// <summary>Claude's own session record.</summary>
    Record,

    /// <summary>The aw:fleet hook's state file.</summary>
    Hook,

    /// <summary>Only the transcript's modification time: the row says it is estimated.</summary>
    Transcript,
}
