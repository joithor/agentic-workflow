namespace Aw.Sindri.Infrastructure.Locks;

/// <summary>The TS sindri's holder record, the part a reclaim reads ('&lt;slot&gt;.holder.json'; the other fields are skipped).</summary>
/// <param name="Pid">The holder's process id.</param>
/// <param name="Host">The host name it wrote (os.hostname(), possibly with a domain).</param>
/// <param name="StartedAt">When the holder took the slot (ISO 8601), or null.</param>
internal sealed record TsHolderRecord(int Pid, string Host, string? StartedAt = null);
