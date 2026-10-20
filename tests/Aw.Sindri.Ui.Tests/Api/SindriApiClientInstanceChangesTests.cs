using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>The instance-change stream over HTTP (API §5): the opt-in query, the open signal, and a frame read from its data (it has no id).</summary>
[Lane(TestLane.Unit)]
public sealed class SindriApiClientInstanceChangesTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly InMemoryHumanToken _token = new(A.HumanToken);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_stream_says_it_is_open_then_yields_each_frame()
    {
        var server = new FakeSindriApi();
        var client = new SindriApiClient(new HttpClient(new FakeSindriHandler(server)), _token, TimeProvider.System);
        var seen = new List<InstanceStreamItem>();

        await foreach (var item in client.InstanceChanges(_api, instance: null, Token))
        {
            seen.Add(item);
            if (seen.Count == 1)
            {
                server.PushChange(new InstanceChange("ITM-1/project/1", "build", 4211));
            }
            else
            {
                server.Drop();
            }
        }

        Assert.Null(seen[0].Change);
        Assert.Equal(new InstanceChange("ITM-1/project/1", "build", 4211), seen[1].Change);
    }

    [Fact]
    public async Task It_opts_in_by_type_and_names_the_instance_when_one_is_asked_for()
    {
        var server = new FakeSindriApi();
        var handler = new FakeSindriHandler(server);
        var client = new SindriApiClient(new HttpClient(handler), _token, TimeProvider.System);

        IReadOnlyList<string?> asked = [];
        await foreach (var _ in client.InstanceChanges(_api, "ITM-1/project/1", Token))
        {
            asked = server.ChangeQueries;
            server.Drop();
        }

        var request = Assert.Single(handler.Requests);
        Assert.Equal("/sindri/events", request.Path);
        Assert.Equal("?types=workflow.instance-changed&instance=ITM-1%2Fproject%2F1", request.Query);
        Assert.Equal(["ITM-1/project/1"], asked);
    }

    [Fact]
    public async Task A_stream_that_isnt_ok_ends_at_once()
    {
        var handler = new FakeSindriHandler(new FakeSindriApi()) { Override = (System.Net.HttpStatusCode.NotFound, "{}") };

        var items = await new SindriApiClient(new HttpClient(handler), _token, TimeProvider.System).InstanceChanges(_api, instance: null, Token).ToListAsync(Token);

        Assert.Empty(items);
    }
}
