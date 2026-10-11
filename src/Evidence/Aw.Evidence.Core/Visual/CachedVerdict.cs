using Aw.Evidence.Core.Ids;
using Aw.Evidence.Core.Judging;

namespace Aw.Evidence.Core.Visual;

/// <summary>A judge's opinion kept for reuse, with the run invocation that formed it.</summary>
/// <param name="Decision">The verdict.</param>
/// <param name="Reasons">Why.</param>
/// <param name="RunInstance">The run invocation that asked the judge.</param>
public sealed record CachedVerdict(VisualDecision Decision, IReadOnlyList<string> Reasons, RunInstanceId RunInstance);
