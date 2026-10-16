#pragma warning disable MA0182 // red stub: used once unit C is implemented
namespace Aw.Sindri.Features.Jobs;

/// <summary>The per-job cap on 'job.progress' events (API P6, Q3): <see cref="Core.Jobs.JobLimits.MaxProgressEvents"/> lines, one '(progress truncated)' line, then nothing.</summary>
internal sealed class JobProgressBudget
{
    /// <summary>What to do with the next line.</summary>
    /// <returns>The decision.</returns>
    public JobAdmission Admit() => throw new NotSupportedException("red until API P6 unit C");

    /// <summary>How many events were admitted (written or the truncation notice): the round of the event being written.</summary>
    public int Written { get; private set; }
}
