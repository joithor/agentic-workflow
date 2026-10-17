namespace Aw.Sindri.Host.Commands;

/// <summary>What a job frame says to the follower: a progress line (<see cref="Round"/> of it), or the end of the job.</summary>
/// <param name="Round">The progress line's number, or 0 for an end.</param>
/// <param name="Text">The line, or the end in words ('succeeded: ok', 'failed: … SND-…', 'cancelled').</param>
/// <param name="Exit">The exit code an end means (0 succeeded, 1 failed or cancelled); 0 for a line.</param>
/// <param name="Ended">Whether the job ended.</param>
internal sealed record JobSignal(int Round, string Text, int Exit, bool Ended);
