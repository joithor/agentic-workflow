namespace Aw.Context.Core.Eval;

/// <summary>A read and checked probe file.</summary>
/// <param name="Path">Its absolute path.</param>
/// <param name="Hash">The SHA-256 of its bytes (lower-case hex): runs of the same file are compared with each other only.</param>
/// <param name="Set">Its probes.</param>
public sealed record ProbeFile(string Path, string Hash, ProbeSet Set);
