using System.Text.Json;

namespace Aw.Sindri.Core.Workflow.Engine.Evals;

/// <summary>Writes a case in the §9.2 format: one JSON object per line, members that are null left out.</summary>
public static class EvalLines
{
    /// <summary>The case's lines, each ending in '\n'.</summary>
    /// <param name="evalCase">The case.</param>
    /// <param name="options">The package's JSON options (they know the eval line records).</param>
    /// <returns>The file's text.</returns>
    public static string Write(EvalCase evalCase, JsonSerializerOptions options) =>
        throw new NotSupportedException("P3 unit (f): EvalLines.Write");
}
