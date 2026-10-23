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
/// item is the root of its latest attempt. A session resolves an address the same way in both forms: an instance on
/// no item is SND-ITEM-404 (a session sees only its repo's items, so the address doesn't resolve for it).
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
    public async Task An_agent_session_cannot_resolve_an_instance_on_no_item_in_either_form()
    {
        await using var served = await Served.Start(_harness.Home, server.Environment, TestContext.Current.CancellationToken);
        var address = await TakenOver(served, "T-71");

        var (shortExit, shortError) = await Short.Run(served.Sindri, "agent-session", "handback", "T-71");
        var longForm = await served.AsSession("agent-session", "handback", address);

        Assert.Equal(longForm.Exit, shortExit);
        Assert.StartsWith("SND-ITEM-404 ", shortError, StringComparison.Ordinal);   // both forms resolve the address first, and a session can't see an instance on no item (API spec 3.8 scope)
        Assert.StartsWith("SND-ITEM-404 ", longForm.Stderr, StringComparison.Ordinal);
        Assert.Equal("taken-over", (await served.Until(address, static v => v.State == "taken-over")).State);
    }

    [Fact]
    public async Task An_agent_session_in_the_items_repo_may_take_an_instance_over_though_it_cannot_hand_it_back()
    {
        await using var served = await Served.Start(_harness.Home, server.Environment, TestContext.Current.CancellationToken);
        var item = await ItemIn(served, "demo");
        var started = await served.Cli("start", "workflow.step", "--item", item, "--fact", "fact:spec=" + _harness.File("spec.md", "the spec"));
        Assert.True(started.Exit == 0, started.Stderr);
        var address = item + "/step/1";
        _ = await served.Until(address, static v => v.Task is not null);

        var taken = await served.AsSessionIn("agent-session", "demo", "takeover", address);

        Assert.True(taken.Exit == 0, taken.Stderr);   // takeover is class `pause`: an agent session may take over, so the attach hook works (only handback is class `desk`)
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
