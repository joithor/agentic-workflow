using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Views;

/// <summary>
/// Folds 'profile.approved' and 'profile.checked' into the 'profile' view (API spec §2.3): one row, the approved hash and
/// when, the live hash the last check saw, and whether (and where) the live profile differs from the approval.
/// </summary>
/// <param name="events">Reads the payloads through the package's AwJson options.</param>
internal sealed class ProfileProjection(EventFactory events) : IProjection
{
    /// <summary>The one row's key.</summary>
    public const string Key = "profile";

    public IReadOnlyList<string> Views => [ViewNames.Profile];

    public bool Handles(string eventType) =>
        string.Equals(eventType, EventTypes.ProfileApproved, StringComparison.Ordinal) || string.Equals(eventType, EventTypes.ProfileChecked, StringComparison.Ordinal);

    public async ValueTask<Result<Unit>> Apply(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recorded);
        ArgumentNullException.ThrowIfNull(rows);
        var row = await rows.Get<ProfileRow>(ViewNames.Profile, Key, cancellationToken).ConfigureAwait(false);
        if (row.IsError)
        {
            return row.Error;
        }

        var next = string.Equals(recorded.Envelope.Type, EventTypes.ProfileApproved, StringComparison.Ordinal)
            ? events.Payload<ProfileApproved>(recorded).Map(approved => Fold(row.Value, approved, recorded))
            : events.Payload<ProfileChecked>(recorded).Map(check => Fold(row.Value, check, recorded));
        return next.IsError ? next.Error : await rows.Put(ViewNames.Profile, Key, next.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The row after an approval.</summary>
    /// <param name="row">The row so far, or null.</param>
    /// <param name="approved">The payload.</param>
    /// <param name="recorded">The recorded event.</param>
    /// <returns>The new row.</returns>
    public static ProfileRow Fold(ProfileRow? row, ProfileApproved approved, LedgerEvent recorded)
        => throw new NotSupportedException("ProfileProjection.Fold: P2 unit C");

    /// <summary>The row after a check.</summary>
    /// <param name="row">The row so far, or null.</param>
    /// <param name="check">The payload.</param>
    /// <param name="recorded">The recorded event.</param>
    /// <returns>The new row.</returns>
    public static ProfileRow Fold(ProfileRow? row, ProfileChecked check, LedgerEvent recorded)
        => throw new NotSupportedException("ProfileProjection.Fold: P2 unit C");
}
