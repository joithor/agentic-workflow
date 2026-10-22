using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Ids;
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
        var packs = await ResolvePacks(cancellationToken).ConfigureAwait(false);
        var row = await views.Get<ProfileRow>(ViewNames.Profile, ViewNames.Profile, cancellationToken).ConfigureAwait(false);
        if (row.IsError)
        {
            return row.Error;
        }

        return row.Value is { } r
            ? new GetProfileResponse(r.Approved, r.ApprovedAt, r.Live, r.CheckedAt, r.Changed, r.ChangedPaths, asOf, packs)
            : new GetProfileResponse(Approved: null, ApprovedAt: null, Live: null, CheckedAt: null, Changed: false, ChangedPaths: [], asOf, packs);
    }

    // The default pack of each loop, then every pack a repo entry names: one row per kind and id, in that order.
    private async ValueTask<IReadOnlyList<ProfilePack>> ResolvePacks(CancellationToken cancellationToken)
    {
        var loaded = await approved.Latest(cancellationToken).ConfigureAwait(false);
        var lenses = loaded.IsError ? null : loaded.Value?.Profile.Workflow?.Packs?.Lenses;
        var packs = new List<ProfilePack>();
        foreach (var (kind, choice, fallback) in new[]
        {
            (LensPackKind.Design, lenses?.Design, "design-five"),
            (LensPackKind.Review, lenses?.Review, "code-review"),
        })
        {
            await Add(packs, kind, repo: null, choice?.Default ?? fallback, cancellationToken).ConfigureAwait(false);
            foreach (var (repo, id) in choice?.Repos?.OrderBy(static r => r.Key, StringComparer.Ordinal) ?? [])
            {
                await Add(packs, kind, RepoName.TryFrom(repo, out var named) ? named : null, id, cancellationToken).ConfigureAwait(false);
            }
        }

        return packs;
    }

    private async ValueTask Add(List<ProfilePack> packs, LensPackKind kind, RepoName? repo, string named, CancellationToken cancellationToken)
    {
        var key = kind == LensPackKind.Design ? "lenses.design" : "lenses.review";
        var resolved = await lensPacks.Resolve(kind, repo, cancellationToken).ConfigureAwait(false);
        var pack = resolved.IsError
            ? new ProfilePack(key, named, "built-in", Hash: null, "none", "invalid: " + resolved.Error.Message)
            : new ProfilePack(key, resolved.Value.Id.Value, "built-in", resolved.Value.Hash, "built-in", "ok");
        if (!packs.Exists(p => string.Equals(p.Kind, pack.Kind, StringComparison.Ordinal) && string.Equals(p.Id, pack.Id, StringComparison.Ordinal)))
        {
            packs.Add(pack);
        }
    }
}
