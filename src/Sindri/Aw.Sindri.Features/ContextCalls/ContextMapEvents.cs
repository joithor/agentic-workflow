using System.Globalization;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The ledger entries of the map's build and promotion, one place for the jobs and the promote command to append them.</summary>
internal static class ContextMapEvents
{
    /// <summary>Appends 'context.map.built' for a build that began at <paramref name="slot"/>.</summary>
    internal static async ValueTask<Result<ContextMapBuilt>> Built(ILedgerStore ledger, EventFactory events, LeaseGrant grant, MapBuildReport report, DateTimeOffset slot, CancellationToken cancellationToken)
    {
        var payload = new ContextMapBuilt(report.Draft.Repo.Value, report.Draft.Number, report.Proposed.Count, report.Degraded);
        var key = string.Create(CultureInfo.InvariantCulture, $"{EventTypes.ContextMapBuilt}:{grant.Epoch}:{slot:O}");
        var appended = await ledger.Append(events.Entry(EventTypes.ContextMapBuilt, payload, key), grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : payload;
    }

    /// <summary>Appends 'context.map.promoted' for a promotion.</summary>
    internal static async ValueTask<Result<LedgerAppend>> Promoted(ILedgerStore ledger, EventFactory events, LeaseGrant grant, MapPromoted promoted, CancellationToken cancellationToken)
    {
        var version = promoted.Version;
        var payload = new ContextMapPromoted(version.Number, version.Repo.Value, promoted.Retained?.Number);
        var key = string.Create(CultureInfo.InvariantCulture, $"{EventTypes.ContextMapPromoted}:{version.Repo.Value}:v{version.Number}");
        return await ledger.Append(events.Entry(EventTypes.ContextMapPromoted, payload, key), grant, cancellationToken).ConfigureAwait(false);
    }
}
