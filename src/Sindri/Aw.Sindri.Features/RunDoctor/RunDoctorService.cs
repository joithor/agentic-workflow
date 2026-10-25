using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.RunDoctor;

internal sealed class RunDoctorService(
    ILease lease, IProcessProbe probe, LedgerFacts ledger, ProfileFacts profile, IControlPlaneProbe api, ITemporalServer temporal, TimeProvider clock, IBuildStamps? builds = null)
    : IService<RunDoctorRequest, RunDoctorResponse>
{
    public async ValueTask<Result<RunDoctorResponse>> Handle(RunDoctorRequest request, CancellationToken cancellationToken)
    {
        if (builds is not null)
        {
            throw new NotSupportedException("the stale build check (#1032) is not built yet");   // red first: doctor warns when the stamps differ
        }

        var now = clock.GetUtcNow();
        var read = await lease.Read(cancellationToken).ConfigureAwait(false);
        var row = read.IsSuccess ? read.Value : null;
        var (views, head) = await ledger.Views(cancellationToken).ConfigureAwait(false);
        var (live, saved, recorded) = await profile.Read(cancellationToken).ConfigureAwait(false);
        var liveness = row is null ? HolderLiveness.Unknown : LeaseDecision.Liveness(row.Holder, probe.Self(), probe.Facts(row.Holder.Pid));
        DoctorCheck[] checks =
        [
            read.IsError ? new DoctorCheck("lease", CheckState.Fail, read.Error.Message, "check the ledger file") : DoctorRules.Lease(row, now, liveness),
            DoctorRules.Ledger(await ledger.Schema(cancellationToken).ConfigureAwait(false)),
            DoctorRules.Views(views, head),
            DoctorRules.Profile(live, saved, recorded),
            DoctorRules.Api(await api.Check(cancellationToken).ConfigureAwait(false), row, now),
            DoctorRules.Temporal(await temporal.Inspect(cancellationToken).ConfigureAwait(false)),
            .. await profile.Context(saved.IsSuccess ? saved.Value : null, cancellationToken).ConfigureAwait(false),
        ];
        return new RunDoctorResponse(checks, DoctorRules.ExitCode(checks));
    }
}
