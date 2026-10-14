using Aw.Kernel.Services;
using Aw.Kernel.Testing;
using Aw.Sindri.Features.GetInstance;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>'workflow status' resolves its reference as the write verbs do: an address, an id or a bare item (spec §6.1; review run 1, code-reviewer-3).</summary>
[Lane(TestLane.Unit)]
public sealed class StatusCommandTests
{
    [Theory]
    [InlineData("T-3/step/1")]
    [InlineData("T-3")]
    public async Task Status_of_an_address_or_a_bare_item_prints_the_instance(string reference)
    {
        var (exit, stdout, stderr) = await Run(reference);

        Assert.Equal((0, string.Empty), (exit, stderr));
        Assert.Contains("T-3/step/1", stdout, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("T-9")]
    [InlineData("T-9/step/1")]
    public async Task Status_of_an_unknown_instance_is_SND_WF_001_with_exit_2(string reference)
    {
        var (exit, stdout, stderr) = await Run(reference);

        Assert.Equal((2, string.Empty), (exit, stdout));
        Assert.StartsWith("SND-WF-001 ", stderr, StringComparison.Ordinal);
    }

    private static async Task<(int Exit, string Stdout, string Stderr)> Run(string reference)
    {
        var instances = new OneInstance(StatusCases.View("done"));
        await using var services = new ServiceCollection()
            .AddSingleton<IService<GetInstanceRequest, GetInstanceResponse>>(new GetInstanceService(instances, instances))
            .AddSingleton<TimeProvider>(new FakeTimeProvider(StatusCases.Now))
            .BuildServiceProvider();
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exit = await StatusCommand.Run([reference], services, new Output(stdout, stderr, SliceHarness.Json), TestContext.Current.CancellationToken);

        return (exit, stdout.ToString(), stderr.ToString());
    }
}
