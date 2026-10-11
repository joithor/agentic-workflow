using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.Views;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>
/// The window under the headless platform, bound to a view model following the in-memory API. Plain xUnit v3 facts that
/// dispatch onto the headless session's UI thread: Avalonia.Headless.XUnit 12.1.3 is built against xUnit v3 3.2 and fails
/// discovery under the repo's 4.0 (docs/csharp/sindri-ui.md "Tests").
/// </summary>
[Lane(TestLane.Integration)]
public sealed class MainWindowTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new();

    private static async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(MainWindowTests).Assembly)
            .Dispatch(async () =>
            {
                await body();
                return true;
            }, TestContext.Current.CancellationToken);

    private async Task<MainWindow> Show(Func<MainWindow, bool> ready)
    {
        var vm = new MainViewModel(_records, _server, new FakeTimeProvider(A.Noon));
        var window = new MainWindow { DataContext = vm };
        window.Show();
        _ = vm.Run(TestContext.Current.CancellationToken);
        for (var i = 0; i < 200 && !ready(window); i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.True(ready(window), "the window never reached the expected state");
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static string Text(Window window, string name) => window.FindControl<TextBlock>(name)?.Text ?? "<missing>";

    private static bool Visible(Window window, string name) => window.FindControl<Control>(name)?.IsEffectivelyVisible == true;

    private static void Capture(Window window, string name)
    {
        var frame = window.CaptureRenderedFrame();
        var dir = Path.Combine(AppContext.BaseDirectory, "frames");
        _ = Directory.CreateDirectory(dir);
        frame?.Save(Path.Combine(dir, name + ".png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    }

    [Fact]
    public Task Not_running_shows_the_word_and_the_sindri_fix() => OnUi(async () =>
    {
        var window = await Show(w => Text(w, "HeadlineText") == "Not running");

        Assert.Equal("sindri", Text(window, "FixText"));
        Assert.True(Visible(window, "NotRunningPanel"));
        Assert.False(Visible(window, "RunningPanel"));
        Assert.Equal("Not running", Avalonia.Automation.AutomationProperties.GetName(window.FindControl<TextBlock>("HeadlineText")!));
        Capture(window, "not-running");
    });

    [Fact]
    public Task Running_shows_lease_profile_services_and_the_last_event() => OnUi(async () =>
    {
        _records.Write(A.Record());
        _server.Push(A.Event(3));
        var window = await Show(w => Text(w, "HeadlineText") == "Running" && Text(w, "LastEventText").EndsWith("#3", StringComparison.Ordinal));

        Assert.Equal("pid 4242 · epoch 7", Text(window, "LeaseText"));
        Assert.Equal("approved", Text(window, "ProfileStateText"));
        Assert.False(Visible(window, "ProfileFixText"));
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "last run 12:00:00Z");
        Assert.False(Visible(window, "NotRunningPanel"));
        Capture(window, "running");
    });

    [Fact]
    public Task A_profile_that_needs_approval_shows_the_approve_command() => OnUi(async () =>
    {
        _records.Write(A.Record());
        _server.Current = A.Status(profile: A.ChangedProfile());
        var window = await Show(w => Text(w, "ProfileStateText") == "changed since approval");

        Assert.Equal("sindri profile approve", Text(window, "ProfileFixText"));
        Assert.True(Visible(window, "ProfileFixText"));
        Capture(window, "profile-changed");
    });
}
