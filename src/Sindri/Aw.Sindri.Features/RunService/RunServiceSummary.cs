using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.RunService;

/// <summary>The summary of a finished RunService job: the service's own, else 'ok: worked' or 'ok: idle'.</summary>
internal static class RunServiceSummary
{
    internal static string Of(ScheduledResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Summary ?? (result.Idle ? "ok: idle" : "ok: worked");
    }
}
