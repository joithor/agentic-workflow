using Aw.Evidence.Core.Usage;

namespace Aw.Evidence.Core.Judging;

/// <summary>A visual critique's answer.</summary>
/// <param name="Decision">The verdict.</param>
/// <param name="Reasons">Why, for the reviewer.</param>
/// <param name="Usage">What the model reported about the call.</param>
public sealed record VisualVerdict(VisualDecision Decision, IReadOnlyList<string> Reasons, InvocationUsage? Usage);
