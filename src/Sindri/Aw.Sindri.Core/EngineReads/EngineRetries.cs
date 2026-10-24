namespace Aw.Sindri.Core.EngineReads;

/// <summary>An activity's retries (API spec §3.8, E-30).</summary>
/// <param name="Attempt">The attempt the pending activity is on.</param>
/// <param name="LastError">The last attempt's failure message, when there was one.</param>
public sealed record EngineRetries(int Attempt, string? LastError);
