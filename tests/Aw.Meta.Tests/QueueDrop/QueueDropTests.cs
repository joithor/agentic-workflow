using Aw.Meta.Core;
using Aw.Meta.Features.QueueDrop;
using Aw.Meta.Features.Status;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.QueueDrop;

public sealed class QueueDropTests
{
    private readonly MetaWorld _world = new();

    private QueueDropService Drop() => new(_world.Messages, _world.Values, _world.Log, _world.Clock);

    private StatusService Service() => new(_world.Config, new StatusReader(_world.Reader, _world.Messages, _world.Gates, _world.Owner, _world.Lanes), _world.Values, _world.Clock);

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

    [Theory, Trait("Lane", "unit")]
    [InlineData(0, "x")]
    [InlineData(5, " ")]
    public void A_bad_number_or_a_blank_reason_is_refused(int pr, string reason) =>
        Assert.Equal(MetaErrors.Invalid.Code, new QueueDropValidator().Validate(new QueueDropRequest(pr, reason)).Error.Code);
}
