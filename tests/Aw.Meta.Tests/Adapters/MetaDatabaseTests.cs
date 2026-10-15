using Aw.Meta.Core.Ids;
using Aw.Meta.Infrastructure.Sqlite;
using Aw.Meta.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Meta.Tests.Adapters;

public sealed class MetaDatabaseTests : IDisposable
{
    private readonly TempDirectory _dir = new();

    private SqliteMessageStore Store() => new(new MetaDatabase(MetaDatabase.OptionsFor(_dir.Path), new FakeTimeProvider()));

    [Fact, Trait("Lane", "integration")]
    public async Task The_first_use_creates_meta_db_and_data_survives_reopening()
    {
        _ = await Store().RecordInbound("/t/a.jsonl", 12, [A.Inbox("m1")], CancellationToken.None);

        var reopened = Store();
        var offset = await reopened.Offset("/t/a.jsonl", CancellationToken.None);
        var messages = await reopened.Messages(CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(_dir.Path, "meta.db")));
        Assert.Equal(12, offset.Value);
        Assert.Equal([MessageId.From("m1")], messages.Value.Select(m => m.Id));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_state_directory_that_cannot_be_created_is_an_error_not_an_exception()
    {
        var blocker = Path.Combine(_dir.Path, "file");
        await File.WriteAllTextAsync(blocker, "x", TestContext.Current.CancellationToken);
        var store = new SqliteMessageStore(new MetaDatabase(MetaDatabase.OptionsFor(Path.Combine(blocker, "meta")), new FakeTimeProvider()));

        var offset = await store.Offset("/t/a.jsonl", CancellationToken.None);

        Assert.True(offset.IsError);
    }

    public void Dispose() => _dir.Dispose();
}
