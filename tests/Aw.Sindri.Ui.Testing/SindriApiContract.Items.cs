using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The contract (<see cref="SindriApiContract"/>, continued): the P3 routes answer as the others do (L13).</summary>
public abstract partial class SindriApiContract
{
    // One call per P3 route (queue, item, timeline, CreateItem, CloseItem), in that order; each answers an error in these theories.
    private static async Task<Error[]> P3Errors(ISindriApi api) =>
    [
        (await api.Queue(_api, Token)).Error,
        (await api.Item(_api, A.NewItem, Token)).Error,
        (await api.Timeline(_api, A.NewItem, after: 0, Token)).Error,
        (await api.CreateItem(_api, new CreateItemRequest("Never sent", Repo: null, Body: null, Link: null), Token)).Error,
        (await api.CloseItem(_api, A.NewItem, new CloseItemRequest("done", 1), Token)).Error,
    ];

    // A command is recorded only when the server was there to receive it: nothing answering, or the caller check, comes first.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_command_that_was_unreachable_or_refused_is_never_recorded(bool down)
    {
        var server = new FakeSindriApi { Refusal = down ? null : A.NoTokenRefusal };
        if (down)
        {
            server.GoDown();
        }

        _ = await P3Errors(Reach(server));

        Expect.Sequence([], server.Created);
        Expect.Sequence([], server.Closed);
    }

    // L13 (arch-2, testing-1): read-your-writes (API §2.4) holds for the P3 routes as for status and services: an accepted
    // CloseItem's sequence is used up by the next view read, which is SND-API-011 while the views lag, then a read that shows what Sindri says.
    [Fact]
    public async Task A_view_read_after_an_accepted_close_is_views_behind_once_when_the_views_lag()
    {
        var api = Reach(new FakeSindriApi { Lag = ViewsLag.Behind });
        _ = await api.CloseItem(_api, A.NewItem, new CloseItemRequest("done", 1), Token);

        var first = await api.Item(_api, A.NewItem, Token);

        Expect.Equal(("SND-API-011", ErrorKind.Unavailable), (first.Error.Code, first.Error.Kind));
        Expect.True((await api.Item(_api, A.NewItem, Token)).IsSuccess);
    }

    // An accepted CreateItem, too, and the queue is the view that waits.
    [Fact]
    public async Task A_queue_read_after_an_accepted_create_is_views_behind_once_when_the_views_lag()
    {
        var api = Reach(new FakeSindriApi { Lag = ViewsLag.Behind });
        _ = await api.CreateItem(_api, new CreateItemRequest("Proof", Repo: null, Body: null, Link: null), Token);

        Expect.Equal("SND-API-011", (await api.Queue(_api, Token)).Error.Code);
        Expect.True((await api.Queue(_api, Token)).IsSuccess);
    }
}
