using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Meta.Core;
using Aw.Meta.Core.Gate;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Sqlite;

/// <summary>IGateStore over meta.db (the gates, gate_steps and merges tables).</summary>
/// <param name="database">The meta database.</param>
internal sealed class SqliteGateStore(MetaDatabase database) : IGateStore
{
    public ValueTask<Result<GateRecord>> Save(GateRecord gate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gate);
        return database.Write(
            async work =>
            {
                var next = await SqliteQueries.One(work, GateSql.NextRun(gate.Pr), new RowMap<int>(r => checked((int)r.GetInt64(0))), cancellationToken).ConfigureAwait(false);
                if (next.IsError)
                {
                    return next.Error;
                }

                var stored = gate with { Run = next.Value };
                foreach (var statement in GateSql.Insert(stored))
                {
                    var changed = await SqliteQueries.Execute(work, statement, cancellationToken).ConfigureAwait(false);
                    if (changed.IsError)
                    {
                        return changed.Error;
                    }
                }

                return Results.Success(stored);
            },
            cancellationToken);
    }

    public async ValueTask<Result<GateRecord?>> Latest(int pr, CancellationToken cancellationToken)
    {
        var heads = await database.Query(GateSql.Latest(pr), GateSql.ReadHead, cancellationToken).ConfigureAwait(false);
        if (heads.IsError || heads.Value.Count == 0)
        {
            return heads.IsError ? heads.Error : Results.Success((GateRecord?)null);
        }

        var head = heads.Value[0];
        var steps = await database.Query(GateSql.Steps(pr, head.Run), GateSql.ReadStep, cancellationToken).ConfigureAwait(false);
        return steps.IsError ? steps.Error : Results.Success<GateRecord?>(head with { Steps = steps.Value });
    }

    public ValueTask<Result<Unit>> Classify(int pr, int run, string step, FlakeVerdict verdict, CancellationToken cancellationToken) =>
        database.Write(
            async work =>
            {
                var changed = await SqliteQueries.Execute(work, GateSql.Classify(pr, run, step, verdict), cancellationToken).ConfigureAwait(false);
                return changed.IsError ? changed.Error
                    : changed.Value == 0 ? MetaErrors.GateStepNotFound.With(string.Create(CultureInfo.InvariantCulture, $"no failed step {step} in gate {pr}/{run}"))
                    : Results.Ok();
            },
            cancellationToken);

    public ValueTask<Result<Unit>> RecordMerge(MergeRecord merge, CancellationToken cancellationToken) =>
        database.Run(GateSql.InsertMerge(merge), cancellationToken);

    public async ValueTask<Result<MergeRecord?>> LatestMerge(CancellationToken cancellationToken)
    {
        var rows = await database.Query(
            GateSql.LatestMerge,
            r => new MergeRecord(checked((int)r.GetInt64(0)), r.GetString(1), SqliteRead.GetTimestamp(r, 2)),
            cancellationToken).ConfigureAwait(false);
        return rows.IsError ? rows.Error : Results.Success(rows.Value.Count == 0 ? null : rows.Value[0]);
    }
}
