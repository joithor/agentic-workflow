using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>The System overview says what its cards say: the headline folds in the Code index and overdue services, and the Agents card counts working sessions.</summary>
[Lane(TestLane.Unit)]
public sealed class SystemHonestyTests
{
    private const string MissingIndex = """{"repos":[{"repo":"repo","state":"missing","commit":null,"builtAt":null,"layers":[],"counts":null}],"health":[]}""";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ServiceList Services(params ServiceRowView[] rows) => new(null, new AsOfView(5, 5, A.Noon, Rebuilding: false, Stalled: false), rows);

    private static ServiceRowView Row(string every, TimeSpan? nextAgo, bool paused = false, string? job = null) =>
        new("pr-watch", every, paused, NextSlot: nextAgo is { } ago ? A.Noon - ago : null, Job: job);

    [Fact]
    public async Task A_healthy_server_with_a_code_index_that_needs_a_look_does_not_say_everything_is_running()
    {
        var system = new SystemTestHarness();
        await system.Show();
        Assert.Equal(Labels.AllRunning, system.Vm.Sentence);
        Assert.False(system.Vm.ToneWarn);

        system.Server.CodeIndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, MissingIndex);
        await system.Vm.CodeIndex.Refresh(A.Record().Url, Token);

        Assert.Equal("Something needs a look: code index.", system.Vm.Sentence);
        Assert.True(system.Vm.ToneWarn);
        Assert.False(system.Vm.ToneError);
    }

    [Fact]
    public async Task An_overdue_service_changes_the_headline_and_the_services_summary()
    {
        var system = new SystemTestHarness();
        await system.Show();

        system.Vm.ShowViews(Services(Row("5s", TimeSpan.FromMinutes(2)), Row("5s", TimeSpan.FromSeconds(1)) with { Name = "setup" }));

        Assert.Equal("Something needs a look: 1 service overdue.", system.Vm.Sentence);
        Assert.True(system.Vm.ToneWarn);
        Assert.Equal("1 overdue · 1 scheduled", system.Vm.ServicesScheduled);
        Assert.Equal([Labels.ServiceOverdue, Labels.ServiceScheduled], system.Vm.ServiceTable.Select(r => r.Status), StringComparer.Ordinal);
        Assert.True(system.Vm.ServiceTable[0].IsOverdue);
    }

    [Fact]
    public async Task The_headline_names_both_when_the_index_and_a_service_need_a_look()
    {
        var system = new SystemTestHarness();
        await system.Show();
        system.Server.CodeIndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, MissingIndex);
        await system.Vm.CodeIndex.Refresh(A.Record().Url, Token);

        system.Vm.ShowViews(Services(Row("5s", TimeSpan.FromMinutes(2))));

        Assert.Equal("Something needs a look: code index, 1 service overdue.", system.Vm.Sentence);
        Assert.Equal("1 overdue", system.Vm.ServicesScheduled);
    }

    [Theory]
    [InlineData("5s", 25, false)]    // within the 30 s floor
    [InlineData("5s", 31, true)]
    [InlineData("1m", 100, false)]   // within twice its cadence
    [InlineData("1m", 121, true)]
    [InlineData("soon", 3600, false)]   // a cadence this build cannot read is never overdue
    public async Task A_service_is_overdue_past_twice_its_cadence_and_at_least_thirty_seconds(string every, int secondsAgo, bool overdue)
    {
        var system = new SystemTestHarness();
        await system.Show();

        system.Vm.ShowViews(Services(Row(every, TimeSpan.FromSeconds(secondsAgo))));

        Assert.Equal(overdue, system.Vm.ServiceTable.Single().IsOverdue);
    }

    [Fact]
    public async Task A_paused_or_running_service_or_one_with_no_next_slot_is_not_overdue()
    {
        var system = new SystemTestHarness();
        await system.Show();

        system.Vm.ShowViews(Services(Row("5s", TimeSpan.FromHours(1), paused: true), Row("5s", TimeSpan.FromHours(1), job: "job-1") with { Name = "a" }, Row("5s", nextAgo: null) with { Name = "b" }));

        Assert.All(system.Vm.ServiceTable, r => Assert.False(r.IsOverdue));
        Assert.Equal(Labels.AllRunning, system.Vm.Sentence);
    }
}
