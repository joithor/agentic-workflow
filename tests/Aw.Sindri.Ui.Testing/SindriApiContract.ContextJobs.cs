using Aw.Kernel;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The contract (<see cref="SindriApiContract"/>, continued): the map refresh's start, as the spec-shaped goldens serve it (the API has none yet).</summary>
public abstract partial class SindriApiContract
{
    [Fact]
    public async Task RefreshContextMap_answers_the_started_job()
    {
        var server = new FakeSindriApi();

        var started = (await Reach(server).RefreshContextMap(_api, Token)).Value;

        Expect.Equal(("JOB-000000000000000000000000000000a1", 21L), (started.JobId, started.Accepted.Sequence));
        Expect.Equal(1, server.RefreshContextMapCalls);
    }

    [Fact]
    public async Task RefreshContextMap_while_one_runs_is_a_conflict_with_the_job_code()
    {
        var server = new FakeSindriApi { RefreshContextMapAnswer = RouteAnswer.Problem("context-map-refresh", "refused-running") };

        var result = await Reach(server).RefreshContextMap(_api, Token);

        Expect.Equal(("SND-JOB-409", ErrorKind.Conflict), (result.Error.Code, result.Error.Kind));
    }
}
