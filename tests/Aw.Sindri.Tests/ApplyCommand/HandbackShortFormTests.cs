using Aw.Kernel.Testing;
using Aw.Sindri.Tests.CompleteStep;
using Aw.Sindri.Tests.Workflows;

namespace Aw.Sindri.Tests.ApplyCommand;

/// <summary>
/// 'sindri handback &lt;item-or-address&gt;' (engine spec §6.3, the D §9.3 alias): the short form of 'workflow handback'; a bare
/// item is the root of its latest attempt, and an agent session is refused SND-AUTH-002 as it is for the long form (review fix C2).
/// </summary>
[Lane(TestLane.Integration)]
public sealed class HandbackShortFormTests(DevServer server) : IClassFixture<DevServer>, IDisposable
{
    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task Handback_with_a_bare_item_hands_back_its_latest_attempt()
    {
        await using var served = await Served.Start(_harness.Home, server.Environment);
        var address = await TakenOver(served, "T-70");

        var (exit, stderr) = await Short.Run(served.Sindri, signal: null, "handback", "T-70");

        Assert.True(exit == 0, stderr);
        Assert.NotNull((await served.Until(address, static v => v.Task is not null)).Task);
    }

    [Fact]
    public async Task An_agent_session_is_refused_SND_AUTH_002_as_for_the_long_form()
    {
        await using var served = await Served.Start(_harness.Home, server.Environment);
        var address = await TakenOver(served, "T-71");

        var (shortExit, shortError) = await Short.Run(served.Sindri, "agent-session", "handback", "T-71");
        var longForm = await served.AsSession("agent-session", "handback", address);

        Assert.Equal(longForm.Exit, shortExit);
        Assert.StartsWith("SND-AUTH-002 ", shortError, StringComparison.Ordinal);
        Assert.StartsWith("SND-AUTH-002 ", longForm.Stderr, StringComparison.Ordinal);
        Assert.Equal("taken-over", (await served.Until(address, static v => v.State == "taken-over")).State);
    }

    private async Task<string> TakenOver(Served served, string item)
    {
        var started = await served.Cli("start", "workflow.step", "--item", item, "--fact", "fact:spec=" + _harness.File("spec.md", "the spec"));
        Assert.True(started.Exit == 0, started.Stderr);
        var address = item + "/step/1";
        _ = await served.Until(address, static v => v.Task is not null);
        var taken = await served.Cli("takeover", address);
        Assert.True(taken.Exit == 0, taken.Stderr);
        _ = await served.Until(address, static v => v.State == "taken-over");
        return address;
    }
}
