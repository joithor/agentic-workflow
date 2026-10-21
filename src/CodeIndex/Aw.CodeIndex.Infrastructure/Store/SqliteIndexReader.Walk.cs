using System.Globalization;
using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel;

namespace Aw.CodeIndex.Infrastructure.Store;

internal sealed partial class SqliteIndexReader
{
    private const string EdgeColumns = "e.line, e.dst_name, e.kind, e.basis, e.confidence, e.candidates, ";

    private const string IntoSymbols = "SELECT " + EdgeColumns + HitColumns
        + " FROM edges e JOIN symbols s ON s.id = e.src WHERE e.kind = 'calls' AND e.dst IN (SELECT value FROM json_each($ids)) ORDER BY e.rowid LIMIT $limit";

    private const string OutOfSymbols = "SELECT " + EdgeColumns + HitColumns
        + " FROM edges e LEFT JOIN symbols s ON s.id = e.dst WHERE e.kind = 'calls' AND e.src IN (SELECT value FROM json_each($ids)) ORDER BY e.rowid LIMIT $limit";

    private const string UnresolvedInto = "SELECT COUNT(*) FROM edges WHERE dst IS NULL AND kind = 'calls' AND dst_name = (SELECT name FROM symbols WHERE id = $id)";

    private const string UnresolvedFrom = "SELECT COUNT(*) FROM edges WHERE dst IS NULL AND kind = 'calls' AND src = $id";

    // The members of the types that inherit, at any depth, from the subject member's own type, with its name and arity.
    private const string Implementations = "WITH RECURSIVE subject(id, name, arity, container) AS ( "
        + "SELECT id, name, arity, substr(qualified, 1, length(qualified) - length(name) - 1) FROM symbols WHERE id = $id), "
        + "implementor(id) AS ( "
        + "SELECT e.src FROM edges e JOIN symbols t ON t.id = e.dst, subject "
        + "WHERE e.kind = 'inherits' AND t.qualified = subject.container AND t.kind IN ('class', 'record', 'struct', 'interface', 'enum') "
        + "UNION "
        + "SELECT e.src FROM edges e JOIN implementor ON e.dst = implementor.id WHERE e.kind = 'inherits') "
        + "SELECT m.id FROM implementor JOIN symbols u ON u.id = implementor.id JOIN subject "
        + "JOIN symbols m ON m.qualified = u.qualified || '.' || subject.name AND m.arity = subject.arity "
        + "AND m.kind NOT IN ('class', 'record', 'struct', 'interface', 'enum') AND m.id <> subject.id";

    public ValueTask<Result<CallWalk>> Walk(SymbolRef symbol, CallDirection direction, CallDepth depth, bool includeImplementations, CancellationToken cancellationToken)
        => SqliteFailure.Guard("Walking the call edges", async () => await WalkEdges(symbol, direction, depth, includeImplementations, cancellationToken).ConfigureAwait(false));

    private async ValueTask<Result<CallWalk>> WalkEdges(SymbolRef symbol, CallDirection direction, CallDepth depth, bool includeImplementations, CancellationToken cancellationToken)
    {
        if (await IdOf(symbol, cancellationToken).ConfigureAwait(false) is not { } subject)
        {
            return CodeIndexErrors.SymbolNotFound.With($"No symbol has the ref '{symbol.Value}'.");
        }

        var walk = new WalkState(subject);
        for (var level = 1; level <= (int)depth && walk.Frontier.Count > 0; level++)
        {
            var targets = await Targets(walk, direction, includeImplementations, cancellationToken).ConfigureAwait(false);
            walk.Absorb(await Step(targets, direction, level, cancellationToken).ConfigureAwait(false));
        }

        var unresolved = await CountUnresolved(subject, direction, cancellationToken).ConfigureAwait(false);
        return new CallWalk(walk.Edges, unresolved, walk.Truncated, CallCoverage.Of(await LanguageCoverage(subject, cancellationToken).ConfigureAwait(false), walk.Edges));
    }

    // Callers of a base or interface member also include the callers of its implementations, when asked.
    private async Task<List<long>> Targets(WalkState walk, CallDirection direction, bool includeImplementations, CancellationToken cancellationToken)
    {
        var targets = new List<long>(walk.Frontier);
        if (includeImplementations && direction == CallDirection.Callers)
        {
            targets.AddRange((await ImplementationsOf(walk.Frontier, cancellationToken).ConfigureAwait(false)).Where(walk.Seen.Add));
        }

        return targets;
    }

    private async Task<Coverage> LanguageCoverage(long subject, CancellationToken cancellationToken)
    {
        var command = Sql.Command(connection, "SELECT f.lang FROM symbols s JOIN files f ON f.path = s.file WHERE s.id = $id").With("$id", subject);
        await using (command.ConfigureAwait(false))
        {
            var language = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
            return string.Equals(language, "csharp", StringComparison.Ordinal) ? Coverage.Full : Coverage.Partial;
        }
    }

    private async Task<int> CountUnresolved(long subject, CallDirection direction, CancellationToken cancellationToken)
    {
        var command = Sql.Command(connection, direction == CallDirection.Callers ? UnresolvedInto : UnresolvedFrom).With("$id", subject);
        await using (command.ConfigureAwait(false))
        {
            return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
        }
    }

    private async Task<List<(CallEdge Edge, long? Other)>> Step(List<long> ids, CallDirection direction, int level, CancellationToken cancellationToken)
    {
        var rows = new List<(CallEdge, long?)>();
        var command = Sql.Command(connection, direction == CallDirection.Callers ? IntoSymbols : OutOfSymbols)
            .With("$ids", "[" + string.Join(',', ids.Select(i => i.ToString(CultureInfo.InvariantCulture))) + "]")
            .With("$limit", CodeIndexRules.MaxEdgesPerLevel + 1);
        await using (command.ConfigureAwait(false))
        {
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var other = reader.IsDBNull(6) ? null : ReadHit(reader, 6);
                    var edge = new CallEdge(
                        level,
                        other,
                        reader.GetString(1),
                        StoredNames.KindOf(reader.GetString(2)),
                        StoredNames.BasisOf(reader.GetString(3)),
                        StoredNames.ConfidenceOf(reader.GetString(4)),
                        reader.GetInt32(0),
                        reader.GetInt32(5),
                        StoredNames.BasisOf(reader.GetString(3)) == EdgeBasis.Syntax ? Coverage.Full : Coverage.Partial);
                    rows.Add((edge, reader.IsDBNull(6) ? null : reader.GetInt64(6)));
                }
            }
        }

        return rows;
    }

    private async Task<List<long>> ImplementationsOf(List<long> subjects, CancellationToken cancellationToken)
    {
        var members = new List<long>();
        foreach (var subject in subjects)
        {
            var command = Sql.Command(connection, Implementations).With("$id", subject);
            await using (command.ConfigureAwait(false))
            {
                var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        members.Add(reader.GetInt64(0));
                    }
                }
            }
        }

        return members;
    }

    /// <summary>The walk so far: the edges listed, the symbols already met, and the symbols whose edges the next level follows.</summary>
    private sealed class WalkState(long subject)
    {
        internal HashSet<long> Seen { get; } = [subject];

        internal List<long> Frontier { get; private set; } = [subject];

        internal List<CallEdge> Edges { get; } = [];

        internal bool Truncated { get; private set; }

        internal void Absorb(List<(CallEdge Edge, long? Other)> step)
        {
            Truncated |= step.Count > CodeIndexRules.MaxEdgesPerLevel;
            Frontier = [];
            foreach (var (edge, other) in step.Take(CodeIndexRules.MaxEdgesPerLevel))
            {
                Edges.Add(edge);
                if (other is { } following && Seen.Add(following))
                {
                    Frontier.Add(following);
                }
            }
        }
    }
}
