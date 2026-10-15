using System.ComponentModel;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>The top bar's tabs keep their identity when a count changes, so the current page's highlight stays (proof B4).</summary>
[Lane(TestLane.Unit)]
public sealed class ShellNavTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_queue_tab_stays_selected_across_count_changes()
    {
        var records = new InMemoryEndpointRecords();
        records.Write(A.Record());
        var server = new FakeSindriApi { Catalog = RouteAnswer.Api("commands", "catalog"), QueueView = RouteAnswer.SpecShaped("queue", "extra-p4-waiting") };
        var shell = new ShellViewModel(records, server, new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await Until(() => shell.Queue.Total > 0 && shell.Navigation[1].Count.Length > 0);
        shell.GoTo(PageId.Queue);
        var tabs = shell.Navigation.ToArray();
        var tab = shell.SelectedNav;
        var swaps = new List<string?>();
        shell.PropertyChanged += (_, e) => swaps.Add(e.PropertyName);
        var counts = new List<string?>();
        var observable = (object?)tab as INotifyPropertyChanged;
        if (observable is { } watched)
        {
            watched.PropertyChanged += (_, e) => counts.Add(e.PropertyName);
        }

        foreach (var view in new[] { "extra-p11-running", "extra-p4-waiting", "extra-p11-running" })
        {
            var before = shell.Navigation[1].Count;
            server.QueueView = RouteAnswer.SpecShaped("queue", view);
            server.Push(A.Event(60, "item.created"));
            await Until(() => shell.Navigation[1].Count != before);
            Assert.Same(tab, shell.SelectedNav);
        }

        Assert.Equal(tabs, shell.Navigation);
        Assert.DoesNotContain(swaps, name => name == nameof(ShellViewModel.Navigation));
        Assert.Contains(counts, name => name == "Count");
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 400 && !condition(); i++)
        {
            await Task.Delay(5, Token);
        }

        Assert.True(condition(), "the condition never held");
    }
}
