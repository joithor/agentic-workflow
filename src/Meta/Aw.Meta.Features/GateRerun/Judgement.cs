using Aw.Meta.Core.Gate;

namespace Aw.Meta.Features.GateRerun;

/// <summary>The classification of a failure and the reason for it.</summary>
/// <param name="Verdict">Flake or real.</param>
/// <param name="Reason">Why, in one sentence.</param>
internal sealed record Judgement(FlakeVerdict Verdict, string Reason);
