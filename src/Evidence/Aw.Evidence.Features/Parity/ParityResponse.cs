using Aw.Evidence.Core.Runs;

namespace Aw.Evidence.Features.Parity;

/// <summary>The parity summary as written to parity.json, and each failed geometry check.</summary>
/// <param name="Summary">The summary.</param>
/// <param name="GeometryFailures">One line per failed box check: '&lt;frame&gt; &lt;target&gt;: &lt;failure&gt;'.</param>
public sealed record ParityResponse(ParitySummary Summary, IReadOnlyList<string> GeometryFailures)
{
    /// <summary>Whether every geometry check passed.</summary>
    public bool Passed => GeometryFailures.Count == 0;
}
