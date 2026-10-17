using System.Globalization;
using Aw.Meta.Core;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Features.QueueDrop;
using Aw.Meta.Features.Status;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Reserve;

namespace Aw.Meta.Tests.QueueDrop;

public sealed class QueueDropTests
{
    private readonly MetaWorld _world = new();

    private QueueDropService Drop() => new(_world.Messages, _world.Gates, _world.Values, _world.Log, _world.Clock);

    private StatusService Service() => new(_world.Config, Worlds.StatusReader(_world), _world.Values, _world.Resources, Worlds.Advisor(_world), _world.Clock);

    private async Task Receive(string id, string name, string body)
    {
        _world.Transcripts.Write(MetaWorld.Transcript, MetaWorld.PeerLine(id, name, body));
        _ = await _world.Sync.Run(MetaWorld.Transcript, CancellationToken.None);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Dropping_removes_the_queue_entry_and_logs_the_reason()
    {
        await _world.Ready();
        await Receive("m1", "t1-api", "ready #250 079c2541");
        await Receive("m2", "t1-ui", "ready #251 079c2542");

        var dropped = await Drop().Handle(new QueueDropRequest(250, "a status line, not a ready"), CancellationToken.None);
        var status = await Service().Handle(new StatusRequest(), CancellationToken.None);

        Assert.True(dropped.IsSuccess, dropped.IsError ? dropped.Error.Message : string.Empty);
        Assert.Equal([251], status.Value.MergeQueue.Select(q => q.Pr));
        Assert.Contains(_world.Log.Lines, line => line.Contains("#250", StringComparison.Ordinal) && line.Contains("a status line, not a ready", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pull_request_with_no_queue_entry_is_refused()
    {
        await _world.Ready();

        var dropped = await Drop().Handle(new QueueDropRequest(99, "x"), CancellationToken.None);

        Assert.Equal(MetaErrors.QueueEntryNotFound.Code, dropped.Error.Code);
        Assert.Empty(_world.Log.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_second_drop_finds_nothing_left_to_remove()
    {
        await _world.Ready();
        await Receive("m1", "t1-api", "ready #250 079c2541");
        _ = await Drop().Handle(new QueueDropRequest(250, "x"), CancellationToken.None);

        var again = await Drop().Handle(new QueueDropRequest(250, "x"), CancellationToken.None);

        Assert.Equal(MetaErrors.QueueEntryNotFound.Code, again.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_merged_pull_request_has_no_queue_entry_to_drop()
    {
        await _world.Ready();
        await Receive("m1", "t1-api", "ready #250 079c2541");
        _ = await _world.Gates.RecordMerge(new MergeRecord(250, "abcdef123", A.At(3)), CancellationToken.None);

        var dropped = await Drop().Handle(new QueueDropRequest(250, "x"), CancellationToken.None);

        Assert.Equal(MetaErrors.QueueEntryNotFound.Code, dropped.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pull_request_the_watch_saw_end_has_no_queue_entry_to_drop()
    {
        await _world.Ready();
        await Receive("m1", "t1-api", "ready #250 079c2541");
        _ = await _world.Values.Set(KvKeys.EndedPrs, "250", CancellationToken.None);

        var dropped = await Drop().Handle(new QueueDropRequest(250, "x"), CancellationToken.None);

        Assert.Equal(MetaErrors.QueueEntryNotFound.Code, dropped.Error.Code);
    }

    // The clock reads 2026-10-15 05:00Z when the drop happens. A message is "after" the drop by instant, not by wall-clock digits.
    [Theory, Trait("Lane", "unit")]
    [InlineData("2026-10-15T07:05:00+02:00", true)]    // 05:05Z, after the drop
    [InlineData("2026-10-15T06:55:00+02:00", false)]   // 04:55Z, before it
    [InlineData("2026-10-15T07:00:00+02:00", false)]   // 05:00Z, at it
    [InlineData("2026-10-15T05:00:01Z", true)]
    public async Task Only_a_message_after_the_drop_queues_the_pull_request_again(string at, bool queued)
    {
        await _world.Ready();
        _ = await _world.Messages.RecordInbound("/t/a.jsonl", 1, [A.Inbox("old", "t1-api", "ready #250 079c2541", minute: 1)], CancellationToken.None);
        _ = await Drop().Handle(new QueueDropRequest(250, "x"), CancellationToken.None);
        var later = new InboxMessage(MessageId.From("new"), "uds:/run/t1-api.sock", "t1-api", "ready #250 079c2542", DateTimeOffset.Parse(at, CultureInfo.InvariantCulture), MessageClassifier.Classify("ready #250 079c2542"));
        _ = await _world.Messages.RecordInbound("/t/a.jsonl", 2, [later], CancellationToken.None);

        var status = await Service().Handle(new StatusRequest(), CancellationToken.None);

        Assert.Equal(queued ? [250] : [], status.Value.MergeQueue.Select(q => q.Pr));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(0, "x")]
    [InlineData(5, " ")]
    public void A_bad_number_or_a_blank_reason_is_refused(int pr, string reason) =>
        Assert.Equal(MetaErrors.Invalid.Code, new QueueDropValidator().Validate(new QueueDropRequest(pr, reason)).Error.Code);
}
