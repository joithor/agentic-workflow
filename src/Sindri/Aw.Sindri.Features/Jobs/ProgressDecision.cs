using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Features.Jobs;

/// <summary>What the writer does with the next progress line.</summary>
internal enum ProgressDecision
{
    /// <summary>Record it.</summary>
    Write,

    /// <summary>Record one '(progress truncated)' line instead: the job used up <see cref="JobLimits.MaxProgressEvents"/>.</summary>
    Truncate,

    /// <summary>Drop it.</summary>
    Drop,
}
