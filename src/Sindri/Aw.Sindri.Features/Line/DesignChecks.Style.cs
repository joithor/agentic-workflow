using Aw.Sindri.Core.LensPacks;

namespace Aw.Sindri.Features.Line;

internal static partial class DesignChecks
{
    // DSN-STYLE-001: spec.md (design.spec) follows the pinned style pack: required sections present and in order, each within its word limit, no forbidden phrase, within the line cap.
    private static IEnumerable<LineFinding> StyleFindings(DesignContext c, StylePack style) => throw new NotSupportedException();
}
