using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Features.Jobs;

/// <summary>The per-job cap on 'job.progress' events (API P6, Q3): <see cref="JobLimits.MaxProgressEvents"/> lines, one '(progress truncated)' line, then nothing.</summary>
internal sealed class JobProgressBudget
{
    private bool _noticed;

    /// <summary>How many events were admitted (written or the truncation notice): the round of the event being written.</summary>
    public int Written { get; private set; }

    /// <summary>What to do with the next line.</summary>
    /// <returns>The decision.</returns>
    public ProgressDecision Admit()
    {
        if (Written < JobLimits.MaxProgressEvents)
        {
            Written++;
            return ProgressDecision.Write;
        }

        if (_noticed)
        {
            return ProgressDecision.Drop;
        }

        _noticed = true;
        Written++;
        return ProgressDecision.Truncate;
    }
}
