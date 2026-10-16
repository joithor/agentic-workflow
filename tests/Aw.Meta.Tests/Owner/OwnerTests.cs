using Aw.Meta.Core;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Features.Owner;
using Aw.Meta.Features.OwnerAnswer;
using Aw.Meta.Features.OwnerAsk;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Owner;

public sealed class OwnerTests
{
    private readonly MetaWorld _world = new();

    private ListOwnerService List() => new(_world.Owner);

    private AnswerOwnerService Answer() => new(_world.Owner, _world.Log, _world.Clock);

    private AskOwnerService Ask() => new(_world.Config, _world.Owner, _world.Clock);

    [Fact, Trait("Lane", "unit")]
    public async Task A_lane_s_owner_question_shows_in_the_owner_queue_until_it_is_answered()
    {
        await _world.Ready();
        _world.Transcripts.Write(MetaWorld.Transcript, MetaWorld.PeerLine("m1", "t1-api", "OWNER QUESTION: sqlite or postgres?"));
        _ = await _world.Sync.Run(MetaWorld.Transcript, CancellationToken.None);

        var open = await List().Handle(new ListOwnerRequest(All: false), CancellationToken.None);
        _ = await Answer().Handle(new AnswerOwnerRequest(1, "sqlite"), CancellationToken.None);
        var after = await List().Handle(new ListOwnerRequest(All: false), CancellationToken.None);
        var all = await List().Handle(new ListOwnerRequest(All: true), CancellationToken.None);

        var question = Assert.Single(open.Value.Questions);
        Assert.Equal(("t1-api", "sqlite or postgres?"), (question.AskedBy, question.Text));
        Assert.Empty(after.Value.Questions);
        Assert.Equal("sqlite", Assert.Single(all.Value.Questions).Answer);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Answering_logs_the_decision_and_opens_a_relay_to_the_lane_that_asked()
    {
        await _world.Ready();
        _ = await _world.Owner.Ask("t1-api", "sqlite or postgres?", A.At(1), CancellationToken.None);

        var response = await Answer().Handle(new AnswerOwnerRequest(1, "sqlite"), CancellationToken.None);

        var relay = Assert.Single(await _world.Open());
        Assert.Equal("OWNER ANSWER q1: sqlite", response.Value.Relay);
        Assert.Equal("- OWNER (2026-10-15): sqlite (asked by t1-api)", response.Value.LogLine);
        Assert.Equal([response.Value.LogLine], _world.Log.Lines);
        Assert.Equal((ObligationKind.RelayOwner, "q1"), (relay.Kind, relay.Key));
        Assert.Contains("OWNER ANSWER q1: sqlite", relay.NextCommand, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Answering_an_unknown_question_is_AW_META_006_and_logs_nothing()
    {
        await _world.Ready();

        var response = await Answer().Handle(new AnswerOwnerRequest(7, "x"), CancellationToken.None);

        Assert.Equal(MetaErrors.QuestionNotFound.Code, response.Error.Code);
        Assert.Empty(_world.Log.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_answer_needs_a_question_number_and_text()
    {
        var validator = new AnswerOwnerValidator();

        Assert.True(validator.Validate(new AnswerOwnerRequest(0, "x")).IsError);
        Assert.True(validator.Validate(new AnswerOwnerRequest(1, " ")).IsError);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_meta_can_queue_its_own_question_and_needs_no_relay_when_it_is_answered()
    {
        await _world.Ready();

        var asked = await Ask().Handle(new AskOwnerRequest("merge #190 before #192?"), CancellationToken.None);
        _ = await Answer().Handle(new AnswerOwnerRequest(asked.Value.Number, "yes"), CancellationToken.None);

        Assert.Equal(1, asked.Value.Number);
        Assert.Equal("meta", Assert.Single((await _world.Owner.List(includeAnswered: true, CancellationToken.None)).Value).AskedBy);
        Assert.Empty(await _world.Open());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_question_needs_text() =>
        Assert.True(new AskOwnerValidator().Validate(new AskOwnerRequest(" ")).IsError);
}
