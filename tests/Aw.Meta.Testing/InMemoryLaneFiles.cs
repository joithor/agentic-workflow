using Aw.Kernel;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>The in-memory ILaneFiles: the one fake, kept honest by LaneFilesContract.</summary>
public sealed class InMemoryLaneFiles : ILaneFiles
{
    private readonly Dictionary<string, LaneRecord> _records = new(StringComparer.Ordinal);

    /// <summary>Puts a lane's record in place.</summary>
    /// <param name="record">The record.</param>
    public void Put(LaneRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        _records[record.Lane] = record;
    }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> RecordSession(string lane, Core.Ids.SessionId session, DateTimeOffset at, CancellationToken cancellationToken)
    {
        _records[lane] = (_records.GetValueOrDefault(lane) ?? new LaneRecord(lane, Session: null, Agent: null)) with { Session = session, RecordedAt = at, Ambiguous = false };
        return Done.Unit();
    }

    /// <inheritdoc />
    public ValueTask<Result<LaneRecord?>> Record(string lane, CancellationToken cancellationToken) =>
        Done.Of(_records.GetValueOrDefault(lane));
}
