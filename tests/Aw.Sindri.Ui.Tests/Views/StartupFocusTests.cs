using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>A relaunch restores the last page, and focus starts on its region at once, not after the first Tab.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class StartupFocusTests : IDisposable
{
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly InMemoryPreferencesStore _store = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    [Theory]
    [InlineData("Home")]
    [InlineData("Context")]
    public async Task The_restored_page_holds_focus_when_the_window_opens(string name)
    {
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(Token);
        bound.CancelAfter(TimeSpan.FromSeconds(30));
        _records.Write(A.Record());
        await new Preferences(_store).Update(p => p with { LastPage = name });

        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(StartupFocusTests).Assembly).Dispatch(async () =>
        {
            try
            {
                var shell = new ShellViewModel(_records, _server, _clock, new Preferences(_store), new FakeClipboard());
                await shell.Restore(bound.Token);
                var window = new ShellWindow { DataContext = shell };
                _windows.Add(window);
                window.Show();
                _ = shell.Run(_stop.Token);
                for (var i = 0; i < 20; i++)
                {
                    Dispatcher.UIThread.RunJobs();
                }

                Assert.Equal(Enum.Parse<PageId>(name), shell.CurrentPage);
                Assert.Same(window.Get<Border>("Page"), window.FocusManager?.GetFocusedElement());
            }
            finally
            {
                await _stop.CancelAsync();
                _windows.ForEach(w => w.Close());
                Dispatcher.UIThread.RunJobs();
            }

            return true;
        }, bound.Token);
    }
}
