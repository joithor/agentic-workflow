using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>
/// Capture mode's work, on the UI thread: waits until the shell is connected and its first queue is read, then for each shot goes to the route through
/// the shell's own navigation (<c>GoTo</c>, <c>Navigate</c>: what the rail, the palette and a deep link run), lets the views settle and renders the window
/// to a PNG. Nothing here is a second code path for the screens: the window is the one the app shows.
/// </summary>
internal static class ShellCapture
{
    private static readonly TimeSpan _connect = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan _settle = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan _tick = TimeSpan.FromMilliseconds(25);

    /// <summary>Saves every shot.</summary>
    /// <param name="window">The shell window, shown at the requested size.</param>
    /// <param name="shell">Its view model.</param>
    /// <param name="request">The shots.</param>
    /// <returns>Null when every frame was written, else what went wrong (one line).</returns>
    internal static async Task<string?> Run(ShellWindow window, ShellViewModel shell, CaptureRequest request)
    {
        foreach (var shot in request.Shots)
        {
            if (CaptureRoute.Parse(shot.Route) is null)
            {
                return $"'{shot.Route}' is neither page:<Page> nor a deep link of the app";
            }
        }

        if (!await Until(() => shell.State == ShellState.Normal && !shell.Queue.IsLoading, _connect).ConfigureAwait(true))
        {
            return $"the app never connected to the control plane (state {shell.State})";
        }

        foreach (var shot in request.Shots)
        {
            switch (CaptureRoute.Parse(shot.Route))
            {
                case CaptureRoute.OnPage page:
                    shell.GoTo(page.Page);
                    break;
                case CaptureRoute.OnLink link:
                    shell.Navigate(link.Link);
                    break;
            }

            await Wait(_settle).ConfigureAwait(true);
            if (Save(window, shot.Path) is { } problem)
            {
                return problem;
            }
        }

        return null;
    }

    private static string? Save(ShellWindow window, string path)
    {
        try
        {
            var size = new PixelSize((int)Math.Round(window.Bounds.Width), (int)Math.Round(window.Bounds.Height));
            using var frame = new RenderTargetBitmap(size, new Vector(96, 96));
            frame.Render(window);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            frame.Save(path, new PngBitmapEncoderOptions());
            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return $"{path} was not written: {e.Message}";
        }
    }

    private static async Task Wait(TimeSpan span)
    {
        for (var waited = TimeSpan.Zero; waited < span; waited += _tick)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(_tick).ConfigureAwait(true);
        }

        Dispatcher.UIThread.RunJobs();
    }

    private static async Task<bool> Until(Func<bool> condition, TimeSpan limit)
    {
        for (var waited = TimeSpan.Zero; waited < limit && !condition(); waited += _tick)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(_tick).ConfigureAwait(true);
        }

        return condition();
    }
}
