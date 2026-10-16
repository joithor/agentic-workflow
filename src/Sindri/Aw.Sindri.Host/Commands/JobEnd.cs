namespace Aw.Sindri.Host.Commands;

/// <summary>How following a job ended (API P6 unit H).</summary>
/// <param name="Exit">The exit code.</param>
/// <param name="After">The ledger sequence of the job's last event, so the final job view reflects it; 0 when unknown.</param>
/// <param name="View">The job view the end was read from (polling), or null when the stream said it.</param>
/// <param name="Left">True when the follower stopped following before the job ended (Ctrl-C, the process went away).</param>
internal sealed record JobEnd(int Exit, long After, ReadOnlyMemory<byte>? View = null, bool Left = false);
