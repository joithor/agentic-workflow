namespace Aw.Sindri.Ui.Tests;

/// <summary>
/// The UI context a unit test with no UI thread supplies (view models refuse to post without one): posted work runs at once on
/// the posting thread. Installed for the current thread while it lives, so view models built in that time capture it.
/// </summary>
internal sealed class InlineUiContext : SynchronizationContext, IDisposable
{
    private readonly SynchronizationContext? _previous = Current;

    internal InlineUiContext()
    {
        SetSynchronizationContext(this);
    }

    public override void Post(SendOrPostCallback d, object? state) => d(state);

    public void Dispose() => SetSynchronizationContext(_previous);
}
