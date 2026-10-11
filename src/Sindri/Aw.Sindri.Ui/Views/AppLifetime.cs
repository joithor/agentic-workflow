using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Views;

/// <summary>
/// The desktop app's life around its window (UI spec §5, §15): closing the window keeps the app in the tray and Quit
/// ends it; the tray shows the shell's state; a later launch's message brings the window forward, and a deep link also
/// navigates. A message never runs an action: anything that isn't "focus" or a deep link is ignored.
/// </summary>
[SuppressMessage("Design", "MA0182:Internal type is apparently never used", Justification = "U0 Unit B: SindriApp starts it once Unit A's shell lands (the integrator's one line); until then its tests use it.")]
internal sealed class AppLifetime : IDisposable
{
    private readonly Window _window;
    private readonly ITray _tray;

    /// <summary>Wires the window, the tray and the instance's messages.</summary>
    /// <param name="window">The main window.</param>
    /// <param name="tray">The tray icon.</param>
    /// <param name="instance">The claimed instance whose messages arrive here.</param>
    /// <param name="navigate">Goes where a deep link points (the shell's Navigate).</param>
    /// <param name="shutdown">Ends the app (the lifetime's Shutdown).</param>
    internal AppLifetime(Window window, ITray tray, ISingleInstance instance, Action<DeepLink> navigate, Action shutdown)
    {
        (_window, _tray) = (window, tray);
        throw new NotSupportedException($"{instance}{navigate.Method.Name}{shutdown.Method.Name}{_window}{_tray}");
    }

    /// <summary>What the tray shows for a shell state.</summary>
    /// <param name="state">The shell's state.</param>
    /// <returns>The tray's view.</returns>
    internal static TrayView TrayViewFor(ShellState state) => throw new NotSupportedException($"{state}");

    /// <summary>Shows the shell's state in the tray.</summary>
    /// <param name="state">The state.</param>
    internal void ShowState(ShellState state) => throw new NotSupportedException($"{state}");

    /// <summary>Handles one message from a later launch (on the UI thread).</summary>
    /// <param name="message">The message's text, unparsed.</param>
    internal void Receive(string message) => throw new NotSupportedException(message);

    /// <summary>Reads the instance's messages until cancelled, handling each on the UI thread.</summary>
    /// <param name="cancellationToken">Stops listening.</param>
    /// <returns>A task that ends when cancelled or the instance ends.</returns>
    internal Task Listen(CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
