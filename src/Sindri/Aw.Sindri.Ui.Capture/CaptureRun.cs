using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aw.Sindri.Ui.Capture;

/// <summary>
/// The capture, on the headless UI thread: opens the shell window at the asked size over the running control plane (the real API client, the
/// endpoint record and human token under AW_SINDRI_HOME), waits until the shell is connected, then goes to each route as a click would, lets the
/// views settle and saves the window's rendered frame as a PNG.
/// </summary>
internal sealed class CaptureRun(string home)
{
    private static readonly TimeSpan _connect = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan _settle = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan _tick = TimeSpan.FromMilliseconds(25);

    /// <summary>The home the control plane keeps its files in: AW_SINDRI_HOME, else $AW_STATE_DIR/sindri-app, else ~/.agentic-workflow/sindri-app.</summary>
    /// <param name="environment">Reads an environment variable.</param>
    /// <param name="userHome">The user's home directory.</param>
    /// <returns>The folder.</returns>
    internal static string HomeOf(Func<string, string?> environment, string userHome) =>
        environment("AW_SINDRI_HOME") is { Length: > 0 } explicitHome
            ? explicitHome
            : Path.Combine(environment("AW_STATE_DIR") is { Length: > 0 } root ? root : Path.Combine(userHome, ".agentic-workflow"), "sindri-app");

    /// <summary>Runs the capture.</summary>
    /// <param name="arguments">The size and the screens.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>Null when every frame was written, else what went wrong (one line).</returns>
    internal async Task<string?> RunAsync(CaptureArguments arguments, CancellationToken cancellationToken)
    {
        var routes = new List<(CaptureShot Shot, CaptureRoute Route)>();
        foreach (var shot in arguments.Shots)
        {
            if (CaptureRoute.Parse(shot.Route) is not { } route)
            {
                return $"'{shot.Route}' is neither page:<Page> nor a deep link of the app";
            }

            routes.Add((shot, route));
        }

        var shell = Shell();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        await shell.Restore(cancellationToken).ConfigureAwait(true);
        var window = new ShellWindow { DataContext = shell, Width = arguments.Width, Height = arguments.Height };
        try
        {
            window.Show();
            _ = shell.Run(stop.Token);
            if (!await UntilAsync(window, () => shell.State == ShellState.Normal && !shell.Queue.IsLoading, _connect, cancellationToken).ConfigureAwait(true))
            {
                return $"the shell never connected to the control plane under {home} (state {shell.State})";
            }

            foreach (var (shot, route) in routes)
            {
                Go(shell, route);
                await SettleAsync(window, cancellationToken).ConfigureAwait(true);
                Save(window, shot.Path);
            }

            return null;
        }
        finally
        {
            await stop.CancelAsync().ConfigureAwait(true);
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void Go(ShellViewModel shell, CaptureRoute route)
    {
        switch (route)
        {
            case CaptureRoute.OnPage page:
                shell.GoTo(page.Page);
                break;
            case CaptureRoute.OnLink link:
                shell.Navigate(link.Link);
                break;
        }
    }

    private ShellViewModel Shell()
    {
        var api = new SindriApiClient(Loopback(), new FileToken(Path.Combine(home, "auth", "human.token")), TimeProvider.System, NullLogger.Instance);
        return new ShellViewModel(new FileRecords(Path.Combine(home, "control.json")), api, TimeProvider.System, new Preferences(new MemoryPreferences()), new NoClipboard());
    }

    // The API is on loopback: never through a configured proxy, and a connect that hangs fails in 2 s; the event stream has no timeout of its own.
    private static HttpClient Loopback() =>
        new(new SocketsHttpHandler { UseProxy = false, ConnectTimeout = TimeSpan.FromSeconds(2) }) { Timeout = Timeout.InfiniteTimeSpan };

    private static void Save(Avalonia.Controls.Window window, string path)
    {
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("headless drawing rendered no frame");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        frame.Save(path, new PngBitmapEncoderOptions());
    }

    private static async Task SettleAsync(Avalonia.Controls.Window window, CancellationToken cancellationToken)
    {
        for (var waited = TimeSpan.Zero; waited < _settle; waited += _tick)
        {
            Pump(window);
            await Task.Delay(_tick, cancellationToken).ConfigureAwait(true);
        }

        Pump(window);
    }

    private static async Task<bool> UntilAsync(Avalonia.Controls.Window window, Func<bool> condition, TimeSpan limit, CancellationToken cancellationToken)
    {
        for (var waited = TimeSpan.Zero; waited < limit && !condition(); waited += _tick)
        {
            Pump(window);
            await Task.Delay(_tick, cancellationToken).ConfigureAwait(true);
        }

        Pump(window);
        return condition();
    }

    private static void Pump(Avalonia.Controls.Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}
