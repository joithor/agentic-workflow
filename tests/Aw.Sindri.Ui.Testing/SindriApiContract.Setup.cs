using System.Net;
using Aw.Kernel;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The contract (<see cref="SindriApiContract"/>, continued): the setup read as the goldens serve it, so the fake and the real client both read what Sindri writes (setup-once spec §7).</summary>
public abstract partial class SindriApiContract
{
    [Fact]
    public async Task Setup_answers_the_six_steps_in_the_served_order()
    {
        var golden = GoldenFiles.Node("setup", "first-run");

        var view = (await Reach(new FakeSindriApi { SetupAnswer = RouteAnswer.Api("setup", "first-run") }).Setup(_api, Token)).Value;

        Expect.Sequence(golden["steps"]!.AsArray().Select(s => (string)s!["id"]!), view.Steps.Select(s => s.Id));
        Expect.Sequence(["repo", "profile", "recipes", "mcp", "map", "onyx"], view.Steps.Select(s => s.Id));
        var repo = view.Steps[0];
        Expect.Equal(("Add your repo", "needs-you", "AddRepo", null), (repo.Title, repo.State, repo.Action, repo.Error));
    }

    [Fact]
    public async Task Setup_reads_progress_and_the_repo_lines_of_a_running_step()
    {
        var server = new FakeSindriApi { SetupAnswer = RouteAnswer.Api("setup", "running-progress") };

        var map = (await Reach(server).Setup(_api, Token)).Value.Steps.Single(s => s.Id == "map");

        Expect.Equal(("running", 40, 100, "40 of 100 done."), (map.State, map.Progress!.Done, map.Progress.Total, map.Detail));
        Expect.Equal(("demo", "running"), (map.Items![0].Repo, map.Items[0].State));
    }

    [Fact]
    public async Task Setup_reads_a_failed_steps_code_and_sentence()
    {
        var server = new FakeSindriApi { SetupAnswer = RouteAnswer.Api("setup", "failed") };

        var map = (await Reach(server).Setup(_api, Token)).Value.Steps.Single(s => s.Id == "map");

        Expect.Equal(("failed", "SND-CONTEXT-042", "The last try at building the map didn't finish."), (map.State, map.Error!.Code, map.Error.Sentence));
    }

    [Fact]
    public async Task Setup_reads_a_skipped_onyx_with_its_reason()
    {
        var server = new FakeSindriApi { SetupAnswer = RouteAnswer.Api("setup", "onyx-no-docker") };

        var onyx = (await Reach(server).Setup(_api, Token)).Value.Steps.Single(s => s.Id == "onyx");

        Expect.Equal(("skipped", "Docker isn't installed."), (onyx.State, onyx.Why));
    }

    [Fact]
    public async Task Setup_from_a_sindri_without_the_route_is_a_not_found_refusal()
    {
        var server = new FakeSindriApi { SetupAnswer = RouteAnswer.Problem(HttpStatusCode.NotFound, "SND-API-004", "The API answered without an error code") };

        var result = await Reach(server).Setup(_api, Token);

        Expect.Equal(ErrorKind.NotFound, result.Error.Kind);
    }
}
