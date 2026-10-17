using Avalonia.Threading;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Views;

/// <summary>
/// The single instance's messages from the app's framework start-up on (UI spec §15), so a later launch is answered
/// whatever start-up is doing. Before the window shows, "focus" needs nothing (the window is about to show) and the
/// latest deep link is kept; both are answered at once. Once the window shows, <see cref="Deliver"/> applies the kept
/// link and hands each message to the window's lifetime on the UI thread before the next is read, so the sender's answer
/// means the app acted on it.
/// </summary>
/// <param name="instance">The claimed instance.</param>
internal sealed class InstanceListener(ISingleInstance instance)
{
    // The UI dispatcher, taken where the listener is built (on the UI thread). Listen resumes on pool threads, and the static
    // Dispatcher.UIThread read from one binds an unbound dispatcher to it (the headless tests reset it for every test).
    private readonly Dispatcher _ui = Dispatcher.UIThread;
    private readonly Lock _gate = new();
    private Action<string>? _receive;
    private string? _kept;

    /// <summary>Reads the instance's messages until cancelled or the instance ends.</summary>
    /// <param name="cancellationToken">Stops listening.</param>
    /// <returns>A task that ends then.</returns>
    internal async Task Listen(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var message in instance.Messages(cancellationToken).ConfigureAwait(false))
            {
                if (Receiver(message) is { } receive)
                {
                    // Claimed on the UI thread just before acting: the claim is also the sender's answer, so a message
                    // whose wait ended while the UI thread was busy is never acted on late, and one acted on is never
                    // answered "not handled" (review run 4, julik-frontend-races-reviewer-1).
                    await _ui.InvokeAsync(() =>
                    {
                        if (message.TryAct())
                        {
                            receive(message.Text);
                        }
                    }).GetTask().ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Stopped.
        }
    }

    /// <summary>Hands the messages to the window's lifetime from now on, the deep link kept before the window showed first (on the UI thread).</summary>
    /// <param name="receive">The lifetime's Receive.</param>
    internal void Deliver(Action<string> receive)
    {
        string? kept;
        lock (_gate)
        {
            _receive = receive;
            (kept, _kept) = (_kept, null);
        }

        if (kept is not null)
        {
            receive(kept);
        }
    }

    // Who handles the message: the window's lifetime once it shows; before that nobody: the message is claimed at once
    // (the window is about to show) and the latest deep link is kept.
    private Action<string>? Receiver(InstanceMessage message)
    {
        lock (_gate)
        {
            if (_receive is null && message.TryAct() && DeepLink.TryParse(message.Text, out _))
            {
                _kept = message.Text;
            }

            return _receive;
        }
    }
}
