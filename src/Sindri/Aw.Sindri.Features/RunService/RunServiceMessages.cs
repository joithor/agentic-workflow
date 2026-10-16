using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.RunService;

/// <summary>The one text of the RunService refusal that two places can raise: the admission (inside the gate) and the work (a claim lost after it).</summary>
internal static class RunServiceMessages
{
    /// <summary>SND-JOB-409 without a running job: a scheduled run of the service is in flight.</summary>
    /// <param name="name">The service.</param>
    /// <returns>The error.</returns>
    public static Kernel.Error ScheduledInFlight(ServiceName name) =>
        SindriErrors.JobConflict.With($"A scheduled run of {name.Value} is in flight (fix: wait for it to end, then run it again)");
}
