using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.RunDoctor;

/// <summary>Reads what the doctor's profile check judges: the live profile, the saved approval and the latest recorded one.</summary>
internal sealed class ProfileFacts(IProfileSource live, IApprovals approvals, ILedgerStore ledger, EventFactory events, IContextEngine context)
{
    public async ValueTask<(Result<LoadedProfile> Live, Result<LoadedProfile?> Saved, Result<RecordedApproval?> Recorded)> Read(CancellationToken cancellationToken)
    {
        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        var saved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        var recorded = RecordedApproval.From(await ledger.Latest(EventTypes.ProfileApproved, cancellationToken).ConfigureAwait(false), events);
        return (loaded, saved, recorded);
    }

    /// <summary>The context engine's doctor rows (S-3a): 'context:map' and 'context:retention', as doctor lines.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The two checks.</returns>
    public async ValueTask<DoctorCheck[]> Context(CancellationToken cancellationToken) =>
    [
        Line("context:map", await context.CheckMap(cancellationToken).ConfigureAwait(false)),
        Line("context:retention", await context.CheckRetention(cancellationToken).ConfigureAwait(false)),
    ];

    private static DoctorCheck Line(string name, Result<MapHealth> health) => health.IsError
        ? new DoctorCheck(name, CheckState.Fail, $"{health.Error.Code}: {health.Error.Message}", "check $AW_SINDRI_HOME/context")
        : new DoctorCheck(health.Value.Name, health.Value.State switch
        {
            MapHealthState.Ok => CheckState.Ok,
            MapHealthState.Warn => CheckState.Warn,
            MapHealthState.Fail => CheckState.Fail,
            _ => CheckState.Fail,
        }, health.Value.Detail, health.Value.Fix);
}
