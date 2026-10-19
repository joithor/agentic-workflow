namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>The size and digest of an input's typed result (W6); the result itself lives only in memory.</summary>
/// <param name="Bytes">The UTF-8 size of its scrubbed JSON.</param>
/// <param name="Sha256">The SHA-256 of that JSON, in lower-case hex.</param>
public sealed record InjectionResultEntry(int Bytes, string Sha256);
