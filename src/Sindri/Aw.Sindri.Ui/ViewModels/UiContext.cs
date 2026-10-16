namespace Aw.Sindri.Ui.ViewModels;

/// <summary>
/// The UI thread's context, captured once where a view model is built (which is always on that thread). A timer or a poller
/// calls <see cref="Post"/> so what it changes reaches bound properties and view handlers on the UI thread, never on its own.
/// </summary>
internal sealed class UiContext
{
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;

    /// <summary>Runs an action on the UI thread: inline when already there or when there is no UI context (a test with no UI thread), else posted.</summary>
    /// <param name="action">What to run.</param>
    internal void Post(Action action)
    {
        if (_context is null || SynchronizationContext.Current == _context)
        {
            action();
        }
        else
        {
            _context.Post(_ => action(), state: null);
        }
    }
}
