using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Ids;
using Aw.Meta.Features.SyncInbox;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Infrastructure.Sqlite;
using Aw.Meta.Tests.Adapters;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Meta.Tests.SyncInbox;

public sealed class SyncInboxIntegrationTests : IDisposable
{
    private readonly TempDirectory _dir = new();

    private SyncInboxService Service()
    {
        var database = new MetaDatabase(MetaDatabase.OptionsFor(Path.Combine(_dir.Path, "state")), new FakeTimeProvider());
        return new SyncInboxService(new InboxSync(new FileTranscriptReader(), new SqliteMessageStore(database), new SqliteOwnerStore(database), new FakeTimeProvider()));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_transcript_on_disk_is_synced_into_meta_db_once_even_across_processes_restarts()
    {
        var transcript = Path.Combine(_dir.Path, "session.jsonl");
        var peer = """{"type":"user","origin":{"kind":"peer","from":"uds:/s.sock","msg_id":"m1","name":"t1-api","body":"ready #7 079c2541"},"timestamp":"2026-10-15T04:05:00.000Z"}""";
        await File.WriteAllTextAsync(transcript, "{\"type\":\"assistant\"}\n" + peer + "\n" + peer[..30], TestContext.Current.CancellationToken);
        var request = new SyncInboxRequest(SessionId.From("s-1"), transcript);

        var first = await Service().Handle(request, TestContext.Current.CancellationToken);
        var restarted = await Service().Handle(request, TestContext.Current.CancellationToken);

        Assert.Equal(["m1"], first.Value.NewMessages.Select(id => id.Value), StringComparer.Ordinal);
        Assert.Empty(restarted.Value.NewMessages);
    }

    public void Dispose() => _dir.Dispose();
}
