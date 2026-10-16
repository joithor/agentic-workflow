using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.RunService;

/// <summary>
/// What a RunService start checks inside the gate, so the HTTP caller sees the refusal at once (API P6 unit D): the service is one of the approved
/// profile's (SND-SVC-404; a built-in the profile does not name is not one, owner answer Q4), this build has it (SND-SVC-404), and no scheduled run
/// of it is in flight (SND-JOB-409 without a running job). Two manual runs of one service are the registry's SND-JOB-409.
/// </summary>
internal static class RunServiceAdmission
{
    internal static async ValueTask<Result<ServiceName?>> Check(IServiceProvider services, RunServiceRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name;
        var approved = await services.GetRequiredService<IApprovedProfile>().Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        var named = approved.Value?.Profile.Services?.ContainsKey(name.Value) == true;
        if (!named)
        {
            return SindriErrors.ServiceNotFound.With($"The approved profile has no service '{name.Value}' (fix: sindri services)");
        }

        if (!services.GetServices<IScheduledService>().Any(s => s.Identity() == name))
        {
            return SindriErrors.ServiceNotFound.With($"The profile names service '{name.Value}', which this build doesn't have");
        }

        if (!services.GetRequiredService<ServiceRunGuard>().TryClaim(name, out var claim))
        {
            return SindriErrors.JobConflict.With($"A scheduled run of {name.Value} is in flight (fix: wait for it to end, then run it again)");
        }

        claim.Dispose();   // the work claims it for itself; this was only the look
        Result<ServiceName?> admitted = name;
        return admitted;
    }
}
