using Avalonia.Controls;
using Avalonia.Headless;
using Aw.Kernel.Testing;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>
/// The first headless start-up under load: several test classes reach for the UI session at the same moment, from pool threads,
/// and each builds a window on it. Any of them touching Avalonia off the session thread first binds the dispatcher to the wrong thread.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class HeadlessSessionStartupTests
{
    [Fact]
    public async Task Eight_pool_threads_starting_the_session_at_once_all_build_a_window_on_it()
    {
        using var gate = new Barrier(8);
        var jobs = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            gate.SignalAndWait(TestContext.Current.CancellationToken);
            return HeadlessUnitTestSession.GetOrStartForAssembly(typeof(HeadlessSessionStartupTests).Assembly)
                .Dispatch(() =>
                {
                    var window = new Window { Width = 100, Height = 100 };
                    window.Show();
                    window.Close();
                    return Environment.CurrentManagedThreadId;
                }, TestContext.Current.CancellationToken);
        }, TestContext.Current.CancellationToken)).ToList();

        var threads = await Task.WhenAll(jobs);

        _ = Assert.Single(threads.Distinct());
    }
}
