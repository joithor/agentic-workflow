namespace Aw.Kernel.Testing.Tests;

/// <summary>
/// A repository that returns a fresh copy (as SQL does) of an aggregate without value equality: the default
/// comparison can't see they're the same, and the violation says to override SameEntity.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class SameEntityTests
{
    [Fact]
    public async Task Without_value_equality_the_violation_points_at_SameEntity()
    {
        var e = await Assert.ThrowsAsync<ContractViolationException>(new CopyingNotes().Get_returns_the_seeded_entity);

        Assert.Contains("SameEntity", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Overriding_SameEntity_compares_by_value() =>
        await new CopyingNotesByValue().Get_returns_each_entity_by_its_own_id();

    [Fact]
    public async Task SameEntity_still_catches_a_wrong_entity() =>
        await Assert.ThrowsAsync<ContractViolationException>(new FirstNoteByValue().Get_returns_each_entity_by_its_own_id);

    private class CopyingNotes : ReadRepositoryContract<Note, ItemId>
    {
        private readonly Dictionary<ItemId, Note> _notes = [];

        protected override ValueTask Seed(Note entity, CancellationToken cancellationToken)
        {
            _notes[entity.Id] = entity;
            return ValueTask.CompletedTask;
        }

        protected override ValueTask<Result<Note>> Get(ItemId id, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<Note>>(
                _notes.TryGetValue(id, out var note) ? Pick(id, note).Copy() : KernelErrors.NotFound.With("no note"));

        protected override ValueTask<bool> Exists(ItemId id, CancellationToken cancellationToken) =>
            ValueTask.FromResult(_notes.ContainsKey(id));

        protected override ItemId IdOf(Note entity) => entity.Id;

        protected override Note NewEntity(int seed) => new(ItemId.From(seed), "note " + seed.ToString(System.Globalization.CultureInfo.InvariantCulture));

        protected override ItemId MissingId() => ItemId.From(999);

        protected virtual Note Pick(ItemId id, Note stored) => stored;
    }

    private class CopyingNotesByValue : CopyingNotes
    {
        protected override bool SameEntity(Note stored, Note found) =>
            stored.Id == found.Id && string.Equals(stored.Text, found.Text, StringComparison.Ordinal);
    }

    private sealed class FirstNoteByValue : CopyingNotesByValue
    {
        private Note? _first;

        protected override Note Pick(ItemId id, Note stored) => _first ??= stored;
    }
}
