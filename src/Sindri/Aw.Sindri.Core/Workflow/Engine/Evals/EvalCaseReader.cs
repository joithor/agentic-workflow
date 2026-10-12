using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Sindri.Core.Workflow.Engine.Evals;

/// <summary>Reads an eval file (spec §9.2): line 1 the start, then one trigger line per line, no blank lines between them.</summary>
public static partial class EvalCaseReader
{
    /// <summary>Reads <paramref name="text"/> as the case <paramref name="name"/>.</summary>
    /// <param name="name">The case's name ('happy').</param>
    /// <param name="text">The file's text.</param>
    /// <param name="options">The package's JSON options (they know the eval line records).</param>
    /// <returns>The case, or the first line that isn't in the format.</returns>
    public static EvalReading Read(string name, string text, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');
        if (lines.Length == 0 || lines[0].Length == 0)
        {
            return Invalid(1, "the file is empty; line 1 is { \"start\": { \"inputs\": { ... } } }");
        }

        var start = AwJson.Deserialize<EvalStartLine>(Encoding.UTF8.GetBytes(lines[0]), options);
        if (start.IsError)
        {
            return Invalid(1, "line 1 isn't a start line { \"start\": { \"inputs\": { ... } } }: " + Reason(start.Error));
        }

        var steps = new List<EvalStepLine>(lines.Length - 1);
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim().Length == 0)
            {
                return Invalid(i + 1, "a blank line; every line after the first is a trigger line");
            }

            var step = AwJson.Deserialize<EvalStepLine>(Encoding.UTF8.GetBytes(lines[i]), options);
            if (step.IsError)
            {
                return Invalid(i + 1, "not a trigger line { \"trigger\", \"gates\"?, \"expect\" }: " + Reason(step.Error));
            }

            steps.Add(step.Value);
        }

        return new EvalReading(new EvalCase(name, start.Value, steps), Invalid: null);
    }

    /// <summary>What the parser said, without its record name and its byte position: the case's line number is the position.</summary>
    private static string Reason(Error error) => Position.Replace(Prefix.Replace(error.Message, string.Empty), string.Empty);

    private static EvalReading Invalid(int line, string detail) => new(Case: null, new EvalVerdict(Passed: false, line, detail));

    [GeneratedRegex(@"^Invalid JSON for \w+: ", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Prefix { get; }

    [GeneratedRegex(@" ?\| LineNumber: \d+ \| BytePositionInLine: \d+\.?", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Position { get; }
}
