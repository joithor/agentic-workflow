using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Kernel.Testing;
using Aw.Sindri.Features.ReadInjection;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.GetInstance;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.ReadInjection;

/// <summary>'sindri workflow injection': a client of the injection route, which it never starts; it prints each section under a heading, or the response as JSON.</summary>
[Lane(TestLane.Unit)]
public sealed class InjectionCommandTests
{
    private static readonly InjectionView _view = new("1.1", [new InjectionSectionView("notes", "input.test.a", 5, new string('a', 64), "hello"), new InjectionSectionView("plan", "input.test.a", 4, new string('b', 64), "x\ny\n")]);

    [Fact(Timeout = 30_000)]
    public async Task With_no_control_plane_it_is_SND_API_001_exit_1()
    {
        using var api = ReadApi.Down();

        var (exit, stdout, stderr) = await Run(api, TestContext.Current.CancellationToken, "T-3/step/1");

        Assert.Equal((1, string.Empty), (exit, stdout));
        Assert.StartsWith("SND-API-001 not running: ", stderr, StringComparison.Ordinal);
    }

    [Fact(Timeout = 30_000)]
    public async Task It_prints_each_section_under_a_heading_and_asks_for_the_instance_route()
    {
        using var api = Api(_view);

        var (exit, stdout, _) = await Run(api, TestContext.Current.CancellationToken, "T-3/step/1");

        Assert.Equal((0, "== notes (input.test.a, 5 bytes)\nhello\n== plan (input.test.a, 4 bytes)\nx\ny\n"), (exit, stdout));
        Assert.EndsWith("/injection", api.Calls[^1], StringComparison.Ordinal);
    }

    [Fact(Timeout = 30_000)]
    public async Task Section_is_sent_to_the_route_and_json_prints_the_response()
    {
        using var api = Api(_view);

        var (exit, stdout, _) = await Run(api, TestContext.Current.CancellationToken, "T-3/step/1", "--section", "notes", "--json");

        Assert.Equal(0, exit);
        Assert.EndsWith("/injection?section=notes", api.Calls[^1], StringComparison.Ordinal);
        Assert.Contains("\"run\": \"1.1\"", stdout.Replace("\":\"", "\": \"", StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Theory(Timeout = 30_000)]
    [InlineData()]
    [InlineData("T-3/step/1", "extra")]
    [InlineData("T-3/step/1", "--section")]
    public async Task A_wrong_argument_list_is_SND_CLI_003_with_exit_2(params string[] args)
    {
        using var api = Api(_view);

        var (exit, stdout, stderr) = await Run(api, TestContext.Current.CancellationToken, args);

        Assert.Equal((2, string.Empty), (exit, stdout));
        Assert.StartsWith("SND-CLI-003 ", stderr, StringComparison.Ordinal);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_run_with_no_sections_prints_no_injection()
    {
        using var api = Api(new InjectionView("1.1", []));

        var (exit, stdout, _) = await Run(api, TestContext.Current.CancellationToken, "T-3/step/1");

        Assert.Equal((0, "no injection\n"), (exit, stdout));
    }

    private static ReadApi Api(InjectionView view) => ReadApi.Over(
        new OneInstance(StatusCases.View("desk-task")),
        services => services.AddSingleton<IService<ReadInjectionRequest, InjectionView>>(new Fixed(view)));

    private static async Task<(int Exit, string Stdout, string Stderr)> Run(ReadApi api, CancellationToken ct, params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = await InjectionCommand.Run(args, api.Desk, new Output(stdout, stderr, SliceHarness.Json), ct);
        return (exit, stdout.ToString(), stderr.ToString());
    }

    private sealed class Fixed(InjectionView view) : IService<ReadInjectionRequest, InjectionView>
    {
        public ValueTask<Result<InjectionView>> Handle(ReadInjectionRequest request, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success(view));
    }
}
