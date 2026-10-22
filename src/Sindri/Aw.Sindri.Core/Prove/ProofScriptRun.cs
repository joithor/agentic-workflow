namespace Aw.Sindri.Core.Prove;

/// <summary>How a recorded evidence script ended.</summary>
/// <param name="Passed">Whether it exited 0 within its budget.</param>
/// <param name="Detail">The exit code or reason, and the tail of what it printed.</param>
public sealed record ProofScriptRun(bool Passed, string Detail);
