using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Tests.CompleteStep;
using Aw.Sindri.Tests.Workflows;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.ApplyCommand;

/// <summary>
/// 'sindri handback &lt;item-or-address&gt;' (engine spec §6.3, the D §9.3 alias): the short form of 'workflow handback'; a bare
/// item is the root of its latest attempt. A session resolves an address the same way in both forms, and sees every
/// instance, an instance on no item too.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class HandbackShortFormTests(DevServer server) : IClassFixture<DevServer>, IDisposable
{
    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task Handback_with_a_bare_item_hands_back_its_latest_attempt()
    {
        await using var served = await Served.Start(_harness.Home, server.Environment, TestContext.Current.CancellationToken);
        var address = await TakenOver(served, "T-70");

        var (exit, stderr) = await Short.Run(served.Sindri, signal: null, "handback", "T-70");

        Assert.True(exit == 0, stderr);
        Assert.NotNull((await served.Until(address, static v => v.Task is not null)).Task);
    }

    [Fact]
    public async Task An_agent_session_hands_back_an_instance_on_no_item_in_either_form()
    {
        await using var served = await Served.Start(_harness.Home, server.Environment, TestContext.Current.CancellationToken);
        var shortAddress = await TakenOver(served, "T-71");
        var longAddress = await TakenOver(served, "T-72");

        var (shortExit, shortError) = await Short.Run(served.Sindri, "agent-session", "handback", "T-71");
        var longForm = await served.AsSession("agent-session", "handback", longAddress);

        Assert.True(shortExit == 0, shortError);   // a session sees every instance and runs every desk row (owner q93; #919)
        Assert.True(longForm.Exit == 0, longForm.Stderr);
        Assert.NotNull((await served.Until(shortAddress, static v => v.Task is not null)).Task);
        Assert.NotNull((await served.Until(longAddress, static v => v.Task is not null)).Task);
    }

    [Fact]
    public async Task An_agent_session_in_the_items_repo_may_take_an_instance_over()
    {
        await using var served = await Served.Start(_harness.Home, server.Environment, TestContext.Current.CancellationToken);
        var item = await ItemIn(served, "demo");
        var started = await served.Cli("start", "workflow.step", "--item", item, "--fact", "fact:spec=" + _harness.File("spec.md", "the spec"));
        Assert.True(started.Exit == 0, started.Stderr);
        var address = item + "/step/1";
        _ = await served.Until(address, static v => v.Task is not null);

        var taken = await served.AsSessionIn("demo", "takeover", address);

        Assert.True(taken.Exit == 0, taken.Stderr);   // takeover is class `pause`: an agent session may take over, so the attach hook works
        Assert.Equal("taken-over", (await served.Until(address, static v => v.State == "taken-over")).State);
    }

    /// <summary>An item the ledger holds in <paramref name="repo"/>, as 'item.created' recorded it.</summary>
    internal static async Task<string> ItemIn(Served served, string repo)
    {
        var sindri = served.Sindri;
        var ids = sindri.GetRequiredService<IIdGenerator>();
        var item = ItemId.From("ITM-" + ids.NewId().ToString("N", System.Globalization.CultureInfo.InvariantCulture));
        var created = new ItemCreated(ids.New<CommandId>(), item, "an item", RepoName.From(repo), Link: null, Body: null, Owner: null);
        var appended = await sindri.GetRequiredService<ILedgerStore>().Append(
            sindri.GetRequiredService<EventFactory>().Entry(EventTypes.ItemCreated, created, "item.created:" + item.Value, item.Value),
            sindri.GetRequiredService<HeldLease>().Current!,   // why: Served holds the lease
            TestContext.Current.CancellationToken);
        Assert.True(appended.IsSuccess, appended.IsError ? appended.Error.ToString() : null);
        return item.Value;
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
