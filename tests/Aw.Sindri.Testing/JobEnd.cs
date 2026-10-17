namespace Aw.Sindri.Testing;

/// <summary>How a job's work ended, as a runner records it.</summary>
/// <param name="Succeeded">True when the work returned a result.</param>
/// <param name="Code">The error code of a failed end, or null.</param>
public sealed record JobEnd(bool Succeeded, string? Code);
