using System.Globalization;
using System.Text;

namespace Aw.Sindri.Core.Workflow.Registry;

/// <summary>
/// A unified line diff (engine spec §4.2: the approve preview's diff against the last approved version). Pure: a
/// longest-common-subsequence walk over the lines, printed as hunks with three lines of context.
/// </summary>
internal static class LineDiff
{
    private const int Context = 3;

    /// <summary>The unified diff from <paramref name="before"/> to <paramref name="after"/>.</summary>
    /// <param name="before">The old text.</param>
    /// <param name="after">The new text.</param>
    /// <param name="beforeLabel">The '---' header's label.</param>
    /// <param name="afterLabel">The '+++' header's label.</param>
    /// <returns>The diff; only the two headers when the texts are equal.</returns>
    public static string Unified(string before, string after, string beforeLabel, string afterLabel)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        var ops = Script(Lines(before), Lines(after));
        var text = new StringBuilder().Append("--- ").Append(beforeLabel).Append('\n').Append("+++ ").Append(afterLabel).Append('\n');
        foreach (var (start, end) in Hunks(ops))
        {
            WriteHunk(text, ops, start, end);
        }

        return text.ToString();
    }

    private static string[] Lines(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');

    /// <summary>The edit script: each line kept (' '), removed ('-') or added ('+'), with its line numbers.</summary>
    private static List<(char Op, string Line, int Old, int New)> Script(string[] a, string[] b)
    {
        var lcs = Table(a, b);
        var ops = new List<(char, string, int, int)>();
        int x = 0, y = 0;
        while (x < a.Length || y < b.Length)
        {
            if (x < a.Length && y < b.Length && string.Equals(a[x], b[y], StringComparison.Ordinal))
            {
                ops.Add((' ', a[x], ++x, ++y));
            }
            else if (y < b.Length && (x == a.Length || lcs[x, y + 1] > lcs[x + 1, y]))
            {
                ops.Add(('+', b[y], x, ++y));
            }
            else
            {
                ops.Add(('-', a[x], ++x, y));
            }
        }

        return ops;
    }

    /// <summary>The longest-common-subsequence lengths of every pair of suffixes.</summary>
    private static int[,] Table(string[] a, string[] b)
    {
        var lcs = new int[a.Length + 1, b.Length + 1];
        for (var i = a.Length - 1; i >= 0; i--)
        {
            for (var j = b.Length - 1; j >= 0; j--)
            {
                lcs[i, j] = string.Equals(a[i], b[j], StringComparison.Ordinal) ? lcs[i + 1, j + 1] + 1 : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
            }
        }

        return lcs;
    }

    /// <summary>The hunks: runs of changes with up to three kept lines around them, merged when their context touches.</summary>
    private static List<(int Start, int End)> Hunks(List<(char Op, string Line, int Old, int New)> ops)
    {
        var hunks = new List<(int Start, int End)>();
        for (var i = 0; i < ops.Count; i++)
        {
            if (ops[i].Op == ' ')
            {
                continue;
            }

            var start = Math.Max(0, i - Context);
            var end = Math.Min(ops.Count, i + Context + 1);
            if (hunks.Count > 0 && start <= hunks[^1].End)
            {
                hunks[^1] = (hunks[^1].Start, end);
            }
            else
            {
                hunks.Add((start, end));
            }
        }

        return hunks;
    }

    private static void WriteHunk(StringBuilder text, List<(char Op, string Line, int Old, int New)> ops, int start, int end)
    {
        var slice = ops.GetRange(start, end - start);
        var oldCount = slice.Count(static o => o.Op != '+');
        var newCount = slice.Count(static o => o.Op != '-');
        var oldStart = slice[0].Op == '+' ? slice[0].Old : slice[0].Old - 1;
        var newStart = slice[0].Op == '-' ? slice[0].New : slice[0].New - 1;
        _ = text.Append(string.Create(CultureInfo.InvariantCulture, $"@@ -{oldStart + (oldCount > 0 ? 1 : 0)},{oldCount} +{newStart + (newCount > 0 ? 1 : 0)},{newCount} @@\n"));
        foreach (var (op, line, _, _) in slice)
        {
            _ = text.Append(op).Append(line).Append('\n');
        }
    }
}
