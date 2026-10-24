namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>
/// Capture mode (desktop UI evidence): the app opens its real window at a fixed size, goes to each screen as a click would, waits for the data
/// and saves the window as a PNG, then ends. The head builds it from the command line; the app does the rest.
/// </summary>
/// <param name="Width">The window's width, in pixels.</param>
/// <param name="Height">The window's height, in pixels.</param>
/// <param name="Shots">The screens to save, in order.</param>
/// <param name="Report">Where a problem is reported, one line (standard error on a desktop).</param>
internal sealed record CaptureRequest(int Width, int Height, IReadOnlyList<CaptureShot> Shots, Action<string> Report)
{
    /// <summary>The exit code when a screen could not be captured.</summary>
    internal const int FailedExitCode = 4;
}

/// <summary>One screen to save.</summary>
/// <param name="Route">'page:&lt;Page&gt;' or a deep link of the app.</param>
/// <param name="Path">Where to write the PNG.</param>
internal sealed record CaptureShot(string Route, string Path);
