namespace Aw.Sindri.Ui.ViewModels;

/// <summary>
/// The UI thread's context, captured once where a view model is built (which must be on the UI thread, after the view layer
/// installed its context: SindriApp.Create does). A timer or a poller calls <see cref="PostOrInline"/> so what it
/// changes reaches bound properties and view handlers on the UI thread, never on its own.
/// </summary>
internal sealed class UiContext
{
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;
    private readonly int _builtOn = Environment.CurrentManagedThreadId;

    /// <summary>
    /// Posts an action to the UI thread, or runs it inline when already there. With no UI context captured it runs inline only
    /// on the thread that built it (a unit test with no UI thread); from any other thread it throws, since running there is the
    /// crash this type exists to prevent.
    /// </summary>
    /// <param name="action">What to run.</param>
    /// <exception cref="InvalidOperationException">There is no UI context and this is not the thread that built it.</exception>
    internal void PostOrInline(Action action)
    {
        if (_context is null)
        {
            if (Environment.CurrentManagedThreadId != _builtOn)
            {
                throw new InvalidOperationException("No UI synchronization context was captured: build view models on the UI thread after the view layer installed it.");
            }

            action();
        }
        else if (SynchronizationContext.Current == _context)
        {
            action();
        }
        else
        {
            _context.Post(_ => action(), state: null);
        }
    }
}
