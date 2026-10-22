using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.EngineReads;

namespace Aw.Sindri.Features.GetEngineHistory;

/// <summary>
/// 'GET /engine/instances/{id}/history?page=' or '?address=' (API spec §3.8, E-30): one page of 50 events newest first, each joined to the ledger row that mirrors it
/// (the row whose temporal event id is the event's RecordEvent activity id, in the same run). A page below 1 or not a number, or no id and no address, is SND-API-013;
/// an instance Temporal has no execution for is SND-ENGINE-404.
/// </summary>
/// <param name="engine">The Temporal engine's read side.</param>
/// <param name="mirrored">Which ledger rows mirror which events.</param>
internal sealed class GetEngineHistoryService(IEngineReader engine, IMirroredSequences mirrored) : IService<GetEngineHistoryRequest, EngineHistory>
{
    public async ValueTask<Result<EngineHistory>> Handle(GetEngineHistoryRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var asked = Parse(request);
        if (asked.IsError)
        {
            return asked.Error;
        }

        var (page, reference) = asked.Value;
        var read = await engine.History(reference, page, EngineHistory.PageSize, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var facts = read.Value;
        var rows = await Mirrored(facts, cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return rows.Error;
        }

        var events = facts.Events.Select(e => new EngineEvent(e.EventId, e.Time, e.Type, e.Name, e.Summary,
            e.Activity is { } id && rows.Value.TryGetValue(id, out var sequence) ? sequence : null)).ToList();
        return new EngineHistory(events, page, facts.Total, (long)page * EngineHistory.PageSize < facts.Total ? page + 1 : null);
    }

    private static Result<(int Page, string Reference)> Parse(GetEngineHistoryRequest request)
    {
        var page = 1;
        if (request.Page is { } text && (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out page) || page < 1))
        {
            return SindriErrors.InvalidQuery.With($"page is a number from 1, not '{text}'");
        }

        return (request.Address is { Length: > 0 } address ? address : request.Instance) is { Length: > 0 } reference
            ? (page, reference)
            : SindriErrors.InvalidQuery.With("name the instance by its id in the path or by ?address=");
    }

    private async ValueTask<Result<IReadOnlyDictionary<string, long>>> Mirrored(EngineHistoryFacts facts, CancellationToken cancellationToken)
    {
        var activities = facts.Events.Select(static e => e.Activity).OfType<string>().ToList();
        return activities.Count == 0
            ? Results.Success<IReadOnlyDictionary<string, long>>(new Dictionary<string, long>(StringComparer.Ordinal))
            : await mirrored.Of(facts.Address, facts.Run, activities, cancellationToken).ConfigureAwait(false);
    }
}
