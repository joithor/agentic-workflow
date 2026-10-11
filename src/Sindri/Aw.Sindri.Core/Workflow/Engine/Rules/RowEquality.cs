using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>Structural equality of instance rows and their parts (spec W21): what the delta compares and the fold tests assert.</summary>
[SuppressMessage("Design", "MA0182", Justification = "Red commit: EventFold.Delta uses it in the implementation commit, which removes this attribute")]
internal static class RowEquality
{
    /// <summary>Whether <paramref name="left"/> and <paramref name="right"/> hold the same row, part by part.</summary>
    /// <param name="left">One row.</param>
    /// <param name="right">The other.</param>
    /// <returns>True when every part is equal.</returns>
    public static bool Same(WorkflowInstance left, WorkflowInstance right) =>
        throw new NotSupportedException("P3 unit (e): RowEquality.Same");
}
