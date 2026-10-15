using Aw.Kernel.Data;
using Aw.Meta.Core.Gate;
using Aw.Meta.Core.Obligations;
using Microsoft.Data.Sqlite;

namespace Aw.Meta.Infrastructure.Sqlite;

/// <summary>The gate store's SQL: named constants and the statements that bind them.</summary>
internal static class GateSql
{
    private const char ScopeSeparator = '\n';

    internal static readonly SqlStatement LatestMerge = new(
        "SELECT pr, sha, at FROM merges ORDER BY at DESC, rowid DESC LIMIT 1", Subject: "latest merge");

    internal static SqlStatement NextRun(int pr) => new(
        "SELECT COALESCE(MAX(run), 0) + 1 FROM gates WHERE pr = $pr", c => c.Bind("$pr", pr), "next gate run");

    internal static IEnumerable<SqlStatement> Insert(GateRecord gate)
    {
        yield return new SqlStatement(
            "INSERT INTO gates (pr, run, head, main, scopes, verdict, at) VALUES ($pr, $run, $head, $main, $scopes, $verdict, $at)",
            c => c.Bind("$pr", gate.Pr).Bind("$run", gate.Run).Bind("$head", gate.Head).Bind("$main", gate.Main)
                .Bind("$scopes", string.Join(ScopeSeparator, gate.Scopes)).Bind("$verdict", gate.Verdict.ToString()).Bind("$at", gate.At),
            "gate");
        for (var position = 0; position < gate.Steps.Count; position++)
        {
            var step = gate.Steps[position];
            var at = position;
            yield return new SqlStatement(
                "INSERT INTO gate_steps (pr, run, position, name, verdict, log, flake) VALUES ($pr, $run, $position, $name, $verdict, $log, $flake)",
                c => c.Bind("$pr", gate.Pr).Bind("$run", gate.Run).Bind("$position", at).Bind("$name", step.Name)
                    .Bind("$verdict", step.Verdict.ToString()).Bind("$log", step.Log).Bind("$flake", step.Flake?.ToString()),
                "gate step " + step.Name);
        }
    }

    internal static SqlStatement Latest(int pr) => new(
        "SELECT pr, run, head, main, scopes, verdict, at FROM gates WHERE pr = $pr ORDER BY run DESC LIMIT 1",
        c => c.Bind("$pr", pr),
        "latest gate");

    internal static SqlStatement Steps(int pr, int run) => new(
        "SELECT name, verdict, log, flake FROM gate_steps WHERE pr = $pr AND run = $run ORDER BY position ASC",
        c => c.Bind("$pr", pr).Bind("$run", run),
        "gate steps");

    internal static SqlStatement Classify(int pr, int run, string step, FlakeVerdict verdict) => new(
        "UPDATE gate_steps SET flake = $flake WHERE pr = $pr AND run = $run AND name = $name AND verdict <> 'Pass'",
        c => c.Bind("$flake", verdict.ToString()).Bind("$pr", pr).Bind("$run", run).Bind("$name", step),
        "gate step " + step);

    internal static SqlStatement InsertMerge(MergeRecord merge) => new(
        "INSERT OR REPLACE INTO merges (pr, sha, at) VALUES ($pr, $sha, $at)",
        c => c.Bind("$pr", merge.Pr).Bind("$sha", merge.Sha).Bind("$at", merge.At),
        "merge");

    internal static GateRecord ReadHead(SqliteDataReader r) => new(
        checked((int)r.GetInt64(0)),
        r.GetString(2),
        r.GetString(3),
        r.GetString(4).Split(ScopeSeparator, StringSplitOptions.RemoveEmptyEntries),
        [],
        Enum.Parse<GateVerdict>(r.GetString(5)),
        SqliteRead.GetTimestamp(r, 6))
    {
        Run = checked((int)r.GetInt64(1)),
    };

    internal static GateStep ReadStep(SqliteDataReader r) =>
        new(r.GetString(0), Enum.Parse<GateVerdict>(r.GetString(1)), r.GetString(2))
        {
            Flake = r.IsDBNull(3) ? null : Enum.Parse<FlakeVerdict>(r.GetString(3)),
        };
}
