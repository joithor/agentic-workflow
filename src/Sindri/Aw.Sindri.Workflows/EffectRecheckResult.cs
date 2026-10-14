namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The recheck's answer (engine spec §5.7; engine P6 unit (b)).</summary>
/// <param name="Code">Null when the effect may run; else SND-RECIPE-005 (revoked), SND-RECIPE-006 (drift) or SND-WF-011 (not allowlisted).</param>
internal sealed record EffectRecheckResult(string? Code);
