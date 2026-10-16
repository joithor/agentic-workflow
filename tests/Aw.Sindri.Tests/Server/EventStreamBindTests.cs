using Aw.Sindri.Host.Endpoints;
using Microsoft.AspNetCore.Http;

namespace Aw.Sindri.Tests.Server;

/// <summary>The first cursor an event stream request names (API spec §5.1, C4).</summary>
public sealed class EventStreamBindTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("3", null, 3)]
    [InlineData("3", "1", 3)]
    [InlineData(null, "2", 2)]
    [InlineData(null, null, 0)]
    [InlineData("-1", null, 0)]   // not a sequence: the header is ignored as an unparseable one is
    [InlineData("-1", "2", 2)]
    [InlineData("nope", "2", 2)]
    public void The_cursor_is_the_header_when_it_holds_a_sequence_else_after_else_zero(string? lastEventId, string? after, long expected)
    {
        var context = new DefaultHttpContext();
        if (lastEventId is not null)
        {
            context.Request.Headers["Last-Event-ID"] = lastEventId;
        }

        if (after is not null)
        {
            context.Request.QueryString = new QueryString("?after=" + after);
        }

        var bound = EventStreamOpener.Bind(context.Request);

        Assert.Equal(expected, bound.Value.After);
    }
}
