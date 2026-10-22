using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetProfile;

/// <summary>
/// The 'profile' view's one row (keyed by the view's name): the approved and live hashes, when each was seen, and
/// whether (and where) the live profile changed since approval. Before any approval or check: nulls and 'changed: false'.
/// </summary>
internal sealed class GetProfileService(IViewReader views, IProjectorStatus status, ILensPacks lensPacks, IApprovedProfile approved) : IService<GetProfileRequest, GetProfileResponse>
{
    public async ValueTask<Result<GetProfileResponse>> Handle(GetProfileRequest request, CancellationToken cancellationToken)
    {
        var asOf = status.Current.AsOf;
        var row = await views.Get<ProfileRow>(ViewNames.Profile, ViewNames.Profile, cancellationToken).ConfigureAwait(false);
        if (row.IsError)
        {
            return row.Error;
        }

        return row.Value is { } r
            ? new GetProfileResponse(r.Approved, r.ApprovedAt, r.Live, r.CheckedAt, r.Changed, r.ChangedPaths, asOf, [])
            : new GetProfileResponse(Approved: null, ApprovedAt: null, Live: null, CheckedAt: null, Changed: false, ChangedPaths: [], asOf, Packs: []);
    }
}
