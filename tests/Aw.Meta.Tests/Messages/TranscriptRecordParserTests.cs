using Aw.Meta.Core.Messages;

namespace Aw.Meta.Tests.Messages;

public sealed class TranscriptRecordParserTests
{
    private static readonly DateTimeOffset _fallback = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private const string UserPeer =
        """{"type":"user","origin":{"kind":"peer","from":"uds:/run/user/1000/cc-socks/100149.sock","verifiedPeerPid":100149,"msg_id":"1f95a","name":"agentic-workflow-4c","fromMode":"prompting","body":"CONTEXT lane api"},"promptSource":"system","message":{"role":"user","content":"x"},"uuid":"u1","timestamp":"2026-10-15T03:57:25.087Z"}""";

    private const string Queued =
        """{"type":"attachment","attachment":{"type":"queued_command","prompt":"p","delivery_id":"d-9","origin":{"kind":"peer","from":"uds:/s/1.sock","name":"agentic-workflow-9a","msg_id":"m-2","body":"ready #7 079c2541"}},"timestamp":"2026-10-15T04:00:00.000Z"}""";

    [Fact, Trait("Lane", "unit")]
    public void A_user_record_with_a_peer_origin_is_a_message()
    {
        var message = TranscriptRecordParser.Parse(UserPeer, _fallback);

        Assert.NotNull(message);
        Assert.Equal("1f95a", message.Id.Value);
        Assert.Equal("uds:/run/user/1000/cc-socks/100149.sock", message.From);
        Assert.Equal("agentic-workflow-4c", message.FromName);
        Assert.Equal("CONTEXT lane api", message.Body);
        Assert.Equal(new DateTimeOffset(2026, 10, 15, 3, 57, 25, 87, TimeSpan.Zero), message.At);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_queued_command_attachment_with_a_peer_origin_is_a_message()
    {
        var message = TranscriptRecordParser.Parse(Queued, _fallback);

        Assert.NotNull(message);
        Assert.Equal("m-2", message.Id.Value);
        Assert.Equal("ready #7 079c2541", message.Body);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_queued_command_without_msg_id_falls_back_to_the_delivery_id()
    {
        var line = Queued.Replace("\"msg_id\":\"m-2\",", string.Empty, StringComparison.Ordinal);

        var message = TranscriptRecordParser.Parse(line, _fallback);

        Assert.NotNull(message);
        Assert.Equal("d-9", message.Id.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_record_without_a_timestamp_uses_the_fallback()
    {
        var line = UserPeer.Replace(",\"timestamp\":\"2026-10-15T03:57:25.087Z\"", string.Empty, StringComparison.Ordinal);

        Assert.Equal(_fallback, TranscriptRecordParser.Parse(line, _fallback)?.At);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"type\":\"user\",\"message\":{\"role\":\"user\",\"content\":\"hi\"}}")]
    [InlineData("{\"type\":\"assistant\",\"origin\":{\"kind\":\"peer\",\"msg_id\":\"a\",\"body\":\"b\"}}")]
    [InlineData("{\"type\":\"user\",\"origin\":{\"kind\":\"human\",\"msg_id\":\"a\",\"body\":\"b\"}}")]
    [InlineData("{\"type\":\"user\",\"origin\":{\"kind\":\"peer\",\"body\":\"b\"}}")]
    [InlineData("{\"type\":\"attachment\",\"attachment\":{\"type\":\"file\",\"origin\":{\"kind\":\"peer\",\"msg_id\":\"a\",\"body\":\"b\"}}}")]
    [InlineData("[1,2,3]")]
    [InlineData("{\"type\":\"user\",\"origin\":\"peer\"}")]
    public void Anything_else_is_not_a_message(string line) =>
        Assert.Null(TranscriptRecordParser.Parse(line, _fallback));
}
