namespace Aw.Meta.Core.Health;

/// <summary>One taken heavy-job lock slot.</summary>
/// <param name="Path">The slot's directory.</param>
/// <param name="Pid">The holder's pid from the slot's owner file, or null when it can't be read.</param>
/// <param name="Alive">Whether that process still runs; false for a stale slot.</param>
/// <param name="Holder">What the owner file says (pid, time, session, command), or empty.</param>
public sealed record HeavyLock(string Path, int? Pid, bool Alive, string Holder);
