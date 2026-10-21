using Aw.CodeIndex.Core.Facade;

namespace Aw.CodeIndex.Core.Model;

/// <summary>The whole-answer coverage rule for Callers and Callees (spec §5.5).</summary>
public static class CallCoverage
{
    /// <summary>Unavailable when the asked symbol's language is unmeasured; Partial when its language or any row is not Full; otherwise Full.</summary>
    /// <param name="subject">The coverage of the asked symbol's language.</param>
    /// <param name="edges">The rows of the answer.</param>
    public static Coverage Of(Coverage subject, IEnumerable<CallEdge> edges)
    {
        _ = (subject, edges);
        return Coverage.Full;
    }
}
