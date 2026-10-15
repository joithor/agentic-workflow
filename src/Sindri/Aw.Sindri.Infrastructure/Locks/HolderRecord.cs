namespace Aw.Sindri.Infrastructure.Locks;

/// <summary>A slot's holder record: where it is, its exact text (for compare and delete), the pid, the host (null for our own owner file) and when it was written.</summary>
/// <param name="Path">The record's file.</param>
/// <param name="Text">Its text.</param>
/// <param name="Pid">The holder's pid.</param>
/// <param name="Host">The host it names, or null.</param>
/// <param name="At">When the holder took the slot, or null.</param>
internal sealed record HolderRecord(string Path, string Text, int Pid, string? Host, DateTimeOffset? At);
