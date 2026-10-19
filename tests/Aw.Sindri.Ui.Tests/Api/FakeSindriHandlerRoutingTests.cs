using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Theme;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>How the fake serves a route: the workflow arm answers reads only, and the lanes' anchors sit above it.</summary>
[Lane(TestLane.Unit)]
public sealed class FakeSindriHandlerRoutingTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    // Each row is a route a GET answers, read from the goldens on disk: only the method keeps a POST out.
    public static TheoryData<string> ServedRoutes() =>
    [
        "recipes",   // POST /workflow/instances is StartInstance (U5'), the one workflow write the fake serves; the list of recipes is a read only
        "instances/" + (string)JsonNode.Parse(GoldenFiles.Api("workflow-instance", "project-build"))!["id"]!, // why: an instance golden has its id
        "recipes/" + WorkflowAnswers.NamedRecipeGoldens.Values.First(),
    ];

    [Theory]
    [MemberData(nameof(ServedRoutes))]
    public async Task A_post_under_the_workflow_routes_is_not_answered_as_a_read(string route)
    {
        using var http = new HttpClient(new FakeSindriHandler(new FakeSindriApi()));

        using var response = await http.PostAsync(new Uri(_api, "/sindri/workflow/" + route), new StringContent("{}"), TestContext.Current.CancellationToken);

        Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed, response.StatusCode.ToString());
    }

    // review run 1, arch-11: the P4 read routes answer GETs only.
    [Theory]
    [InlineData("/sindri/decisions")]
    [InlineData("/sindri/needs-you")]
    public async Task A_post_to_a_P4_read_route_is_not_answered_as_a_read(string route)
    {
        using var http = new HttpClient(new FakeSindriHandler(new FakeSindriApi()));

        using var response = await http.PostAsync(new Uri(_api, route), new StringContent("{}"), TestContext.Current.CancellationToken);

        Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed, response.StatusCode.ToString());
    }

    [Fact]
    public void The_lane_anchors_sit_in_the_route_switch_above_the_workflow_read_arm()
    {
        var lines = File.ReadAllLines(Path.Combine(UiSource.Root, "tests", "Aw.Sindri.Ui.Testing", "FakeSindriHandler.cs"));
        var u3 = Array.FindIndex(lines, l => l.Contains("LANE ANCHOR U3'", StringComparison.Ordinal));
        var u8 = Array.FindIndex(lines, l => l.Contains("LANE ANCHOR U8", StringComparison.Ordinal));
        var arm = Array.FindIndex(lines, l => l.Contains("path.StartsWith(\"/sindri/workflow/\"", StringComparison.Ordinal));

        Assert.True(u3 >= 0 && u8 == u3 + 1, "the markers are adjacent");
        Assert.True(u8 < arm, "the markers come before the workflow-read arm");
        Assert.Contains("above the workflow-read arm", lines[u3], StringComparison.Ordinal);
        Assert.Contains("above the workflow-read arm", lines[u8], StringComparison.Ordinal);
        Assert.Contains("== HttpMethod.Get", lines[arm], StringComparison.Ordinal);
    }
}
