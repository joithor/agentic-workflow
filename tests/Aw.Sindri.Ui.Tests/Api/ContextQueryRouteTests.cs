using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>The free-text context query over the canned handler: the question travels as one escaped query value, and the two anchor lists come back.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextQueryRouteTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task QueryAnchors_sends_the_question_as_one_escaped_q_value_and_reads_both_lists()
    {
        var server = new FakeSindriApi { QueryAnswer = _ => FakeSindriApi.QueryAnchorsBody(["feature:a"], ["feature:b"]) };
        var handler = new FakeSindriHandler(server);
        var client = new SindriApiClient(new HttpClient(handler), new InMemoryHumanToken(A.HumanToken), TimeProvider.System);

        var answer = await client.QueryAnchors(_api, "drag & drop shifts", Token);

        var seen = Assert.Single(handler.Requests);
        Assert.Equal(("GET", "/sindri/context/query", "?q=drag%20%26%20drop%20shifts"), (seen.Method, seen.Path, seen.Query));
        Assert.Equal(["feature:a"], answer.Value.Anchors);
        Assert.Equal(["feature:b"], answer.Value.SuggestedAnchors);
    }
}
