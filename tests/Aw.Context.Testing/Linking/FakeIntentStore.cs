using Aw.Context.Core;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Testing.Linking;

/// <summary>The in-memory intent store; <see cref="IntentStoreContract"/> holds it to the SQLite store's behavior.</summary>
public sealed class FakeIntentStore : IIntentStore
{
    private readonly Dictionary<string, IntentRecord> _records = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<IntentLink>> _links = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string? Cursor, DateTimeOffset? LastSync, string? LastError)> _cursors = new(StringComparer.Ordinal);

    /// <summary>When set, every call fails with it (a store that can't open).</summary>
    public Error? Failure { get; set; }

    /// <inheritdoc/>
    public ValueTask<Result<IntentLayer>> Layer(CancellationToken cancellationToken) => Done(() => new IntentLayer(
        [.. _records.Values.OrderBy(r => r.Key.Value, StringComparer.Ordinal)],
        [.. _links.Values.SelectMany(l => l).OrderBy(l => l.Record.Value, StringComparer.Ordinal).ThenBy(l => l.Target.Value, StringComparer.Ordinal).ThenBy(l => l.Rule)],
        [],
        []));

    /// <inheritdoc/>
    public ValueTask<Result<IntentSourceState>> State(SourceId source, CancellationToken cancellationToken) => Done(() =>
    {
        _ = _cursors.TryGetValue(source.Value, out var cursor);
        var mine = _records.Values.Where(r => r.Source.Value == source.Value).Select(r => r.Key.Value).ToHashSet(StringComparer.Ordinal);
        return new IntentSourceState(cursor.Cursor, cursor.LastSync, cursor.LastError, mine.Count, _links.Where(l => mine.Contains(l.Key)).Sum(l => l.Value.Count), 0, mine.Count);
    });

    /// <inheritdoc/>
    public ValueTask<Result<IntentCommit>> CommitPage(IntentPage page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        return Attempt(() =>
        {
            var known = page.Records.Select(r => r.Key.Value).Concat(_records.Keys).ToHashSet(StringComparer.Ordinal);
            if (page.Links.FirstOrDefault(l => !known.Contains(l.Record.Value)) is { } orphan)
            {
                return ContextErrors.StoreUnavailable.With("a link names a record that isn't stored: " + orphan.Record.Value);
            }

            var changed = 0;
            foreach (var record in page.Records)
            {
                var isNew = !_records.TryGetValue(record.Key.Value, out var stored) || stored.Version != record.Version;
                changed += isNew ? 1 : 0;
                _records[record.Key.Value] = isNew ? record : stored!;
                _links[record.Key.Value] = [.. page.Links.Where(l => l.Record.Value == record.Key.Value).DistinctBy(l => (l.Target.Value, l.Rule))];
            }

            _ = _cursors.TryGetValue(page.Source.Value, out var before);
            _cursors[page.Source.Value] = (page.Cursor ?? before.Cursor, page.At, null);
            return Results.Success(new IntentCommit(page.Records.Count, page.Links.Count, changed));
        });
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> RecordFailure(SourceId source, string code, CancellationToken cancellationToken) => Done(() =>
    {
        _ = _cursors.TryGetValue(source.Value, out var before);
        _cursors[source.Value] = (before.Cursor, before.LastSync, code);
        return Unit.Value;
    });

    private ValueTask<Result<T>> Done<T>(Func<T> work) => Attempt(() => Results.Success(work()));

    private ValueTask<Result<T>> Attempt<T>(Func<Result<T>> work) => ValueTask.FromResult(Failure is { } failure ? failure : work());
}
