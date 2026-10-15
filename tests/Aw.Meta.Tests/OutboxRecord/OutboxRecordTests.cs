using Aw.Meta.Core;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Features.OutboxRecord;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.OutboxRecord;

public sealed class OutboxRecordTests
{
    private readonly MetaWorld _world = new();

    private OutboxRecordService Service() => new(_world.Session, _world.Messages, _world.Reader, _world.Clock);

    private async Task<OutboxRecordResponse> Send(string to, string message, string session = MetaWorld.MetaSessionId, string? id = null)
    {
        var request = new OutboxRecordRequest(SessionId.From(session), to, message, id is null ? null : MessageId.From(id));
        var result = await Service().Handle(request, CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    private async Task Receive(string id, string name, string body)
    {
        _world.Transcripts.Write(MetaWorld.Transcript, MetaWorld.PeerLine(id, name, body));
        _ = await _world.Sync.Run(MetaWorld.Transcript, CancellationToken.None);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_send_from_any_other_session_is_not_recorded()
    {
        await _world.Ready();

        var response = await Send("sindri-api", "hi", session: "s-other");

        Assert.False(response.Recorded);
        Assert.Empty((await _world.Messages.Sent(CancellationToken.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_send_to_the_peer_closes_its_reply_obligation_and_names_what_it_closed()
    {
        await _world.Ready();
        await Receive("m1", "sindri-api", "please rebase");
        await Receive("m2", "sindri-ui", "and me");

        var response = await Send("sindri-api", "done", id: "out-1");

        var sent = Assert.Single((await _world.Messages.Sent(CancellationToken.None)).Value);
        Assert.True(response.Recorded);
        Assert.Equal([(ObligationKind.Reply, "m1")], response.Closed.Select(o => (o.Kind, o.Key)));
        Assert.Equal(("sindri-api", "done", "out-1"), (sent.To, sent.Message, sent.Id?.Value));
        Assert.Equal(["m2"], (await _world.Open()).Select(o => o.Key), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_send_addressed_to_the_peer_socket_closes_the_reply_too()
    {
        await _world.Ready();
        await Receive("m1", "sindri-api", "please rebase");

        var response = await Send("uds:/run/sindri-api.sock", "done");

        _ = Assert.Single(response.Closed);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_lane_map_broadcast_closes_only_when_every_lane_has_the_new_version()
    {
        await _world.Ready();
        _world.Sessions.Add(A.Live("sindri-api", "s-api"));
        _world.Sessions.Add(A.Live("sindri-ui", "s-ui"));
        _ = await _world.Refresher.Run((await _world.Config.Load(CancellationToken.None)).Value, _world.Clock.GetUtcNow(), CancellationToken.None);

        var first = await Send("sindri-api", "LANE MAP v1: api=sindri-api, ui=sindri-ui");
        var second = await Send("sindri-ui", "LANE MAP v1: api=sindri-api, ui=sindri-ui");

        Assert.Empty(first.Closed);
        Assert.Equal([ObligationKind.BroadcastLanes], second.Closed.Select(o => o.Kind));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Telling_every_lane_where_main_moved_closes_the_announce_obligation()
    {
        await _world.Ready();
        _world.Sessions.Add(A.Live("sindri-api", "s-api"));
        _world.Sessions.Add(A.Live("sindri-ui", "s-ui"));
        _ = await _world.Refresher.Run((await _world.Config.Load(CancellationToken.None)).Value, _world.Clock.GetUtcNow(), CancellationToken.None);
        _ = await _world.Gates.RecordMerge(new MergeRecord(190, "abc1234def", _world.Clock.GetUtcNow()), CancellationToken.None);
        _ = await Send("sindri-api", "LANE MAP v1: x");
        _ = await Send("sindri-ui", "LANE MAP v1: x");

        var first = await Send("sindri-api", "main moved to abc1234: #190 merged. Merge main into your branches.");
        var second = await Send("sindri-ui", "main moved to abc1234: #190 merged. Merge main into your branches.");

        Assert.Empty(first.Closed);
        Assert.Equal([ObligationKind.AnnounceMain], second.Closed.Select(o => o.Kind));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Relaying_an_owner_answer_to_the_lane_that_asked_closes_the_relay()
    {
        await _world.Ready();
        _ = await _world.Owner.Ask("sindri-api", "sqlite?", A.At(1), CancellationToken.None);
        _ = await _world.Owner.Answer(1, "yes", _world.Clock.GetUtcNow(), CancellationToken.None);
        _world.Clock.Advance(TimeSpan.FromMinutes(1));

        var wrong = await Send("sindri-ui", "OWNER ANSWER q1: yes");
        var right = await Send("sindri-api", "OWNER ANSWER q1: yes");

        Assert.Empty(wrong.Closed);
        Assert.Equal([ObligationKind.RelayOwner], right.Closed.Select(o => o.Kind));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_send_with_no_recipient_or_text_is_refused()
    {
        var validator = new OutboxRecordValidator();

        Assert.True(validator.Validate(new OutboxRecordRequest(SessionId.From("s"), "", "x", Id: null)).IsError);
        Assert.True(validator.Validate(new OutboxRecordRequest(SessionId.From("s"), "a", " ", Id: null)).IsError);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_hook_payload_gives_the_session_recipient_text_and_message_id()
    {
        var parsed = PostToolUseParser.Parse("""
            {"session_id":"s-meta","tool_name":"SendMessage","tool_input":{"to":"sindri-api","message":"done","summary":"s"},"tool_response":{"msg_id":"out-7"}}
            """);

        var request = parsed.Value;
        Assert.NotNull(request);
        Assert.Equal(("s-meta", "sindri-api", "done", "out-7"), (request.Session.Value, request.To, request.Message, request.Id?.Value));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_summary_stands_in_for_a_message_that_is_not_text_and_a_missing_response_has_no_id()
    {
        var parsed = PostToolUseParser.Parse("""{"session_id":"s","tool_input":{"to":"a","message":{"type":"shutdown"},"summary":"closing down"},"tool_response":"ok"}""");

        Assert.Equal(("closing down", null), (parsed.Value?.Message, parsed.Value?.Id?.Value));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"session_id":"s","tool_name":"Bash","tool_input":{"command":"ls"}}""")]
    [InlineData("""{"session_id":"s","tool_input":{"to":"a"}}""")]
    [InlineData("""{"session_id":"s"}""")]
    public void A_call_of_another_tool_is_not_a_send(string json)
    {
        var parsed = PostToolUseParser.Parse(json);

        Assert.True(parsed.IsSuccess);
        Assert.Null(parsed.Value);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{"tool_input":{"to":"a","message":"m"}}""")]
    public void A_payload_that_is_not_an_object_with_a_session_is_refused(string json)
    {
        var parsed = PostToolUseParser.Parse(json);

        Assert.Equal(MetaErrors.Invalid.Code, parsed.Error.Code);
    }
}
