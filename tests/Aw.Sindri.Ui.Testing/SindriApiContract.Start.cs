using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The contract (<see cref="SindriApiContract"/>, continued): the recipe list and StartInstance, as the goldens serve them.</summary>
public abstract partial class SindriApiContract
{
    /// <summary>Every start refusal the API goldens carry.</summary>
    public static TheoryData<string> StartRefusals => [.. StartGoldens.Refusals];

    [Fact]
    public async Task Recipes_answer_each_row_of_the_list_golden_with_its_standing_and_hash()
    {
        var golden = JsonNode.Parse(GoldenFiles.Api("workflow-recipes", "list"))!["recipes"]!.AsArray(); // why: the golden is a list

        var list = (await Reach(new FakeSindriApi()).Recipes(_api, Token)).Value;

        Expect.Sequence(golden.Select(r => (string)r!["id"]!), list.Recipes.Select(r => r.Id)); // why: every row has all of these
        Expect.Sequence(golden.Select(r => (string)r!["standing"]!), list.Recipes.Select(r => r.Standing));
        Expect.Sequence(golden.Select(r => (string)r!["hash"]!), list.Recipes.Select(r => r.Hash));
        Expect.Sequence(golden.Select(r => (int)r!["version"]!), list.Recipes.Select(r => r.Version));
    }

    [Fact]
    public async Task Start_records_the_request_and_answers_the_started_golden_by_id_address_existing_and_finished()
    {
        var golden = GoldenFiles.Node("workflow-instances-start", "started");
        var server = new FakeSindriApi();
        var request = StartGoldens.Request();

        var started = (await Reach(server).StartInstance(_api, request, Token)).Value;

        Expect.Equal(((string)golden["instance"]!["id"]!, (string)golden["instance"]!["address"]!), (started.Instance.Id, started.Instance.Address)); // why: the golden names its instance
        Expect.Equal(((bool)golden["existing"]!, (bool)golden["finished"]!), (started.Existing, started.Finished));
        Expect.Equal(1, server.Started.Count);
        var seen = server.Started[0];
        Expect.Equal((request.Recipe, request.Item, request.NextAttempt), (seen.Recipe, seen.Item, seen.NextAttempt));
        Expect.Sequence(request.Facts.Keys, seen.Facts.Keys);
        foreach (var (key, fact) in request.Facts)
        {
            Expect.Equal(fact.Name, seen.Facts[key].Name);
            Expect.True(fact.Content.Span.SequenceEqual(seen.Facts[key].Content.Span));
        }
    }

    [Fact]
    public async Task Rerun_records_the_instance_and_answers_the_started_golden()
    {
        var golden = GoldenFiles.Node("workflow-instances-start", "started");
        var server = new FakeSindriApi();

        var started = (await Reach(server).RerunInstance(_api, "WFI-failed", Token)).Value;

        Expect.Equal(((string)golden["instance"]!["id"]!, (string)golden["instance"]!["address"]!), (started.Instance.Id, started.Instance.Address)); // why: the rerun answers the shape a start does
        Expect.Sequence(["WFI-failed"], server.Reruns);
    }

    [Theory]
    [MemberData(nameof(StartRefusals))]
    public async Task Start_refusals_are_the_goldens_code_and_detail_as_served(string scenario)
    {
        var golden = GoldenFiles.Node("workflow-instances", scenario);
        var server = new FakeSindriApi { StartAnswer = RouteAnswer.Problem("workflow-instances", scenario) };

        var started = await Reach(server).StartInstance(_api, StartGoldens.Request(), Token);

        Expect.Equal(((string)golden["title"]!, (string)golden["detail"]!), (started.Error.Code, started.Error.Message)); // why: a refusal has both
        Expect.Equal((int)golden["status"]! == (int)HttpStatusCode.Conflict ? ErrorKind.Conflict : ErrorKind.Invalid, started.Error.Kind); // why: as above
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_start_that_was_unreachable_or_refused_is_never_recorded(bool down)
    {
        var server = new FakeSindriApi { Refusal = down ? null : A.NoTokenRefusal };
        if (down)
        {
            server.GoDown();
        }

        var started = await Reach(server).StartInstance(_api, StartGoldens.Request(), Token);

        Expect.True(started.IsError);
        Expect.Sequence([], server.Started);
    }
}
