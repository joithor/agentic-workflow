using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Tests.ApiRun;

/// <summary>What a followed same-origin redirect sends next: 303 turns a write into a GET without the body, 307 keeps both.</summary>
public sealed class ApiRunRedirectTests
{
    private const string Post = """{"requests":[{"method":"POST","path":"/api/notes","body":{"title":"hi"},"expect":{"status":200}}]}""";
    private const string Origin = ApiRunHarness.Origin;

    [Fact, Trait("Lane", "unit")]
    public async Task A_303_is_followed_with_a_GET_and_no_body()
    {
        var harness = new ApiRunHarness().WithScript(Post);
        _ = harness.Transport.Reply("POST", Origin + "/api/notes", new HttpReply(303, "/api/done", string.Empty));
        _ = harness.Transport.Reply("GET", Origin + "/api/done", new HttpReply(200, Location: null, "ok"));

        var response = (await harness.Run()).Value;

        Assert.Equal(ApiOutcome.Passed, response.Summary.Outcome);
        var second = harness.Transport.Calls[1];
        Assert.Equal((HttpVerb.Get, Origin + "/api/done", null), (second.Method, second.Url, second.Body));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_307_is_followed_with_the_same_method_and_body()
    {
        var harness = new ApiRunHarness().WithScript(Post);
        _ = harness.Transport.Reply("POST", Origin + "/api/notes", new HttpReply(307, "/api/again", string.Empty));
        _ = harness.Transport.Reply("POST", Origin + "/api/again", new HttpReply(200, Location: null, "ok"));

        var response = (await harness.Run()).Value;

        Assert.Equal(ApiOutcome.Passed, response.Summary.Outcome);
        var second = harness.Transport.Calls[1];
        Assert.Equal((HttpVerb.Post, Origin + "/api/again", """{"title":"hi"}"""), (second.Method, second.Url, second.Body));
    }
}
