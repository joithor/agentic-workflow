using Aw.Meta.Core.Gate;

namespace Aw.Meta.Features.GateRerun;

/// <summary>The classification of a failure and the reason for it.</summary>
/// <param name="Verdict">Flake or real.</param>
/// <param name="Reason">Why, in one sentence.</param>
/// <param name="AloneLog">The log of the run alone on the gate tree.</param>
/// <param name="MainLog">The log of the run on main, when there was one.</param>
internal sealed record Judgement(FlakeVerdict Verdict, string Reason, string? AloneLog = null, string? MainLog = null);
