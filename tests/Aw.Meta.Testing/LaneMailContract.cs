using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Ports;
using Xunit;

namespace Aw.Meta.Testing;

/// <summary>The ILaneMail contract: the same tests run against the in-memory fake and the SQLite adapter.</summary>
public abstract class LaneMailContract
{
    private static readonly CancellationToken _none = CancellationToken.None;

    /// <summary>A fresh, empty mail store, and the message store a reply lands in.</summary>
    /// <returns>The mail and the messages.</returns>
    protected abstract ValueTask<(ILaneMail Mail, IMessageStore Messages)> Create();

    /// <summary>A send is owed, undelivered and numbered; delivering marks it delivered but still owed.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_send_is_owed_until_the_lane_replies_and_delivery_keeps_it_owed()
    {
        var (mail, _) = await Create();

        var first = await mail.Send("api", "rebase please", A.At(1), _none);
        var second = await mail.Send("api", "and squash", A.At(2), _none);
        var other = await mail.Send("ui", "hello", A.At(3), _none);
        _ = await mail.Deliver([first.Value], A.At(4), _none);
        var owed = await mail.Owed("api", _none);

        Expect.That(first.Value < second.Value && second.Value < other.Value, "numbers rise");
        Expect.That(owed.Value.Select(m => m.Text).SequenceEqual(["rebase please", "and squash"], StringComparer.Ordinal), "owed in order, only this lane's");
        Expect.That(owed.Value[0].Delivered && !owed.Value[1].Delivered, "delivery is per message");
    }

    /// <summary>A reply becomes an inbound message with the lane id and the team-lane sender, and clears the lane's owed messages.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_reply_is_recorded_as_an_inbound_message_and_clears_what_the_lane_owed()
    {
        var (mail, messages) = await Create();
        _ = await mail.Send("api", "status?", A.At(1), _none);

        var first = await mail.Reply(new LaneIdentity("t1", "api"), "ready #12", A.At(2), _none);
        var second = await mail.Reply(new LaneIdentity("t1", "api"), "more", A.At(3), _none);
        var inbox = await messages.Messages(_none);
        var owed = await mail.Owed("api", _none);

        Expect.That(first.Value.Value == "lane:api:1" && second.Value.Value == "lane:api:2", "ids count per lane");
        Expect.That(inbox.Value.Count == 2, "both replies are inbound messages");
        Expect.That(inbox.Value[0].From == "t1-api" && inbox.Value[0].FromName == "t1-api", "from is team-lane");
        Expect.That(inbox.Value[0].Body == "ready #12" && inbox.Value[0].Class.Kind == Core.Messages.MessageKind.Ready && inbox.Value[0].Class.Pr == 12, "classified like any inbound message");
        Expect.That(owed.Value.Count == 0, "nothing owed after a reply");
    }
}
