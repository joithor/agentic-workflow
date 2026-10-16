using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Core.Jobs;

/// <summary>The pure rules for a job's progress lines.</summary>
public static class JobLines
{
    /// <summary>A progress line as it is recorded: scrubbed, ANSI and OSC sequences escaped, folded to one line and cut to <see cref="JobLimits.LineChars"/>.</summary>
    /// <param name="scrubber">Takes secrets out.</param>
    /// <param name="line">What the work reported.</param>
    /// <returns>The clean line.</returns>
    public static string Clean(ITextScrubber scrubber, string line)
    {
        ArgumentNullException.ThrowIfNull(scrubber);
        ArgumentNullException.ThrowIfNull(line);
        return SafeText.Clean(scrubber, line, JobLimits.LineChars);
    }

    /// <summary>The lines after adding <paramref name="next"/>: the last <see cref="JobLimits.ViewLines"/>.</summary>
    /// <param name="lines">The lines so far.</param>
    /// <param name="next">The new line.</param>
    /// <returns>The kept lines, oldest first.</returns>
    public static IReadOnlyList<JobLine> Keep(IReadOnlyList<JobLine> lines, JobLine next)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(next);
        var skip = Math.Max(0, lines.Count + 1 - JobLimits.ViewLines);
        return [.. lines.Skip(skip), next];
    }
}
