using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;


namespace Aw.Sindri.Features.RunService;

/// <summary>
/// What a RunService start checks inside the gate, so the HTTP caller sees the refusal at once (API P6 unit D): the service is one of the approved
/// profile's (SND-SVC-404; a built-in the profile does not name is not one, owner answer Q4), this build has it (SND-SVC-404), and no scheduled run
/// of it is in flight (SND-JOB-409 without a running job). Two manual runs of one service are the registry's SND-JOB-409.
/// </summary>
/// <param name="approvals">The approved profile.</param>
/// <param name="scheduled">Every service this build has.</param>
/// <param name="guard">Says whether a run of the service is in flight.</param>
internal sealed class RunServiceAdmission(IApprovedProfile approvals, IEnumerable<IScheduledService> scheduled, ServiceRunGuard guard) : IJobAdmission<RunServiceRequest>
{
    public async ValueTask<Result<ServiceName?>> Admit(RunServiceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = request.Name;
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        if (approved.Value?.Profile.Services?.ContainsKey(name.Value) != true)
        {
            return SindriErrors.ServiceNotFound.With($"The approved profile has no service '{name.Value}' (fix: sindri services)");
        }

        if (!scheduled.Any(s => s.Identity() == name))
        {
            return SindriErrors.ServiceNotFound.With($"The profile names service '{name.Value}', which this build doesn't have");
        }

        if (!guard.TryClaim(name, out var claim))
        {
            if (guard.IsManualRun(name))
            {
                Result<ServiceName?> running = name;
                return running;   // a manual run: the start refuses with SND-JOB-409 naming that job
            }

            return SindriErrors.JobConflict.With($"A scheduled run of {name.Value} is in flight (fix: wait for it to end, then run it again)");
        }

        claim.Dispose();   // the work claims it for itself; this was only the look
        Result<ServiceName?> admitted = name;
        return admitted;
    }
}
