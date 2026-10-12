using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.RunDoctor;

internal sealed class RunDoctorService(
    ILease lease, IProcessProbe probe, ILedgerStore ledger, ProfileFacts profile, IControlPlaneProbe api, TimeProvider clock, IContextEngine context)
    : IService<RunDoctorRequest, RunDoctorResponse>
{
    public async ValueTask<Result<RunDoctorResponse>> Handle(RunDoctorRequest request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var read = await lease.Read(cancellationToken).ConfigureAwait(false);
        var row = read.IsSuccess ? read.Value : null;
        var (live, saved, recorded) = await profile.Read(cancellationToken).ConfigureAwait(false);
        var liveness = row is null ? HolderLiveness.Unknown : LeaseDecision.Liveness(row.Holder, probe.Self(), probe.Facts(row.Holder.Pid));
        DoctorCheck[] checks =
        [
            read.IsError ? new DoctorCheck("lease", CheckState.Fail, read.Error.Message, "check the ledger file") : DoctorRules.Lease(row, now, liveness),
            DoctorRules.Ledger(await ledger.Schema(cancellationToken).ConfigureAwait(false)),
            DoctorRules.Profile(live, saved, recorded),
            DoctorRules.Api(await api.Check(cancellationToken).ConfigureAwait(false), row, now),
            Check("context:map", await context.CheckMap(cancellationToken).ConfigureAwait(false)),
            Check("context:retention", await context.CheckRetention(cancellationToken).ConfigureAwait(false)),
        ];
        return new RunDoctorResponse(checks, DoctorRules.ExitCode(checks));
    }

    /// <summary>A context engine check as a doctor line (context spec §8.7, §6.2).</summary>
    private static DoctorCheck Check(string name, Result<MapHealth> health) => health.IsError
        ? new DoctorCheck(name, CheckState.Fail, $"{health.Error.Code}: {health.Error.Message}", "check $AW_SINDRI_HOME/context")
        : new DoctorCheck(health.Value.Name, health.Value.State switch
        {
            MapHealthState.Ok => CheckState.Ok,
            MapHealthState.Warn => CheckState.Warn,
            MapHealthState.Fail => CheckState.Fail,
            _ => CheckState.Fail,
        }, health.Value.Detail, health.Value.Fix);
}
