using System.Text.Json;

namespace Aw.Sindri.Core.Workflow.Engine.Evals;

/// <summary>Reads an eval file (spec §9.2): line 1 the start, then one trigger line per line, no blank lines between them.</summary>
public static class EvalCaseReader
{
    /// <summary>Reads <paramref name="text"/> as the case <paramref name="name"/>.</summary>
    /// <param name="name">The case's name ('happy').</param>
    /// <param name="text">The file's text.</param>
    /// <param name="options">The package's JSON options (they know the eval line records).</param>
    /// <returns>The case, or the first line that isn't in the format.</returns>
    public static EvalReading Read(string name, string text, JsonSerializerOptions options) =>
        throw new NotSupportedException("P3 unit (f): EvalCaseReader.Read");
}
