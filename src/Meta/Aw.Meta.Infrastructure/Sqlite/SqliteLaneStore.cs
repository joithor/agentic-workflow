using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Sqlite;

/// <summary>ILaneStore over meta.db (the lanes table holds the current map, lane_map_versions its history).</summary>
/// <param name="database">The meta database.</param>
internal sealed class SqliteLaneStore(MetaDatabase database) : ILaneStore
{
    private static readonly SqlStatement _entries = new(
        "SELECT lane, session_name, session_id, source FROM lanes ORDER BY position ASC", Subject: "lanes");

    private static readonly SqlStatement _version = new("SELECT MAX(version) FROM lane_map_versions", Subject: "lane map version");

    public async ValueTask<Result<LaneMapState>> Current(CancellationToken cancellationToken)
    {
        var entries = await database.Query(
            _entries,
            r => new LaneAssignment(r.GetString(0), r.GetString(1), r.IsDBNull(2) ? null : SessionId.From(r.GetString(2)), Enum.Parse<LaneSource>(r.GetString(3))),
            cancellationToken).ConfigureAwait(false);
        var version = await database.Query(_version, r => r.IsDBNull(0) ? 0 : checked((int)r.GetInt64(0)), cancellationToken).ConfigureAwait(false);
        if (entries.IsError || version.IsError)
        {
            return entries.IsError ? entries.Error : version.Error;
        }

        return new LaneMapState(version.Value.Count == 0 ? 0 : version.Value[0], entries.Value);
    }

    public ValueTask<Result<Unit>> Save(LaneMapState map, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(map);
        return database.Write(
            async work =>
            {
                var statements = new List<SqlStatement>
                {
                    new("DELETE FROM lanes", Subject: "lanes"),
                    new(
                        "INSERT OR REPLACE INTO lane_map_versions (version, at) VALUES ($version, $at)",
                        c => c.Bind("$version", map.Version).Bind("$at", at),
                        "lane map version"),
                };
                statements.AddRange(map.Entries.Select((entry, position) => new SqlStatement(
                    "INSERT INTO lanes (position, lane, session_name, session_id, source) VALUES ($position, $lane, $name, $session, $source)",
                    c => c.Bind("$position", position).Bind("$lane", entry.Lane).Bind("$name", entry.SessionName)
                        .Bind("$session", entry.Session?.Value).Bind("$source", entry.Source.ToString()),
                    "lane " + entry.Lane)));
                foreach (var statement in statements)
                {
                    var changed = await SqliteQueries.Execute(work, statement, cancellationToken).ConfigureAwait(false);
                    if (changed.IsError)
                    {
                        return changed.Error;
                    }
                }

                return Results.Ok();
            },
            cancellationToken);
    }
}
