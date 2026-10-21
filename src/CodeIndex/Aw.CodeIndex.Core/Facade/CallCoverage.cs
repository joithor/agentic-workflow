using Aw.CodeIndex.Core.Model;

namespace Aw.CodeIndex.Core.Facade;

/// <summary>The whole-answer coverage rule for Callers and Callees (spec §5.5).</summary>
public static class CallCoverage
{
    /// <summary>Unavailable when the asked symbol's language is unmeasured; Partial when its language or any row is not Full; otherwise Full.</summary>
    /// <param name="subject">The coverage of the asked symbol's language.</param>
    /// <param name="edges">The rows of the answer.</param>
    public static Coverage Of(Coverage subject, IEnumerable<CallEdge> edges)
        => subject == Coverage.Unavailable ? Coverage.Unavailable
            : subject == Coverage.Partial || edges.Any(e => e.Coverage != Coverage.Full) ? Coverage.Partial
            : Coverage.Full;
}
