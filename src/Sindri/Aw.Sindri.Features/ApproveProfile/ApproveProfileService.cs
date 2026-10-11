using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.ApproveProfile;

internal sealed class ApproveProfileService(IProfileSource live, IApprovals approvals, HeldLease held, ILedgerStore ledger, EventFactory events)
    : IService<ApproveProfileRequest, ApproveProfileResponse>
{
    public async ValueTask<Result<ApproveProfileResponse>> Handle(ApproveProfileRequest request, CancellationToken cancellationToken)
    {
        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        var latest = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return loaded.Error;
        }

        var previous = latest.IsSuccess ? latest.Value : null;
        var shown = new ApproveProfileResponse(loaded.Value.Hash, ProfileDiff.Between(previous?.Profile, loaded.Value.Profile),
            Approved: previous?.Hash == loaded.Value.Hash);
        if (request.Hash is not { } asked || shown.Approved)
        {
            return shown;
        }

        var refusal = Refusal(asked, loaded.Value, request.Confirmed, held.Current);
        if (refusal is not null)
        {
            return refusal;
        }

        var saved = await approvals.Save(loaded.Value, cancellationToken).ConfigureAwait(false);
        var entry = events.Entry(EventTypes.ProfileApproved, new ProfileApproved(asked, shown.Changes), $"{EventTypes.ProfileApproved}:{asked.Value}");
        var recorded = saved.IsError ? saved.Error : (await ledger.Append(entry, held.Current!, cancellationToken).ConfigureAwait(false)).Map(_ => Unit.Value);   // why: Refusal returned an error when no lease is held
        return recorded.IsError ? recorded.Error : shown with { Approved = true };
    }

    private static Error? Refusal(Core.Ids.ProfileHash asked, LoadedProfile live, bool confirmed, LeaseGrant? grant) => true switch
    {
        _ when asked != live.Hash => SindriErrors.ProfileHashMismatch.With(
            $"{asked.Abbreviated}… isn't the live profile's hash ({live.Hash.Value}); run 'sindri profile approve' to see it and what changed"),
        _ when !confirmed => SindriErrors.ProfileNeedsTerminal.With(
            "approving needs you at an interactive terminal: run 'sindri profile approve <hash>' and type the first 6 characters"),
        _ when grant is null => SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)"),
        _ => null,
    };
}
