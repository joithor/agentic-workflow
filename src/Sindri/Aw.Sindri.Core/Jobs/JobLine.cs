namespace Aw.Sindri.Core.Jobs;

/// <summary>One progress line of a job, already cleaned (scrubbed, escaped, one line, at most 200 characters).</summary>
/// <param name="Round">Its number in the job, from 1.</param>
/// <param name="Line">The text.</param>
/// <param name="At">When it was recorded.</param>
public sealed record JobLine(int Round, string Line, DateTimeOffset At);
