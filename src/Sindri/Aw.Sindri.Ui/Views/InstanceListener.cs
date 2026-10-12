using Avalonia.Threading;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Views;

/// <summary>The single instance's messages, read once the window shows (red: as before the fix).</summary>
/// <param name="instance">The claimed instance.</param>
internal sealed class InstanceListener(ISingleInstance instance)
{
    private CancellationToken _stop;

    /// <summary>Reads the instance's messages until cancelled.</summary>
    /// <param name="cancellationToken">Stops listening.</param>
    /// <returns>A task that ends when cancelled or the instance ends.</returns>
    internal Task Listen(CancellationToken cancellationToken)
    {
        _stop = cancellationToken;
        return Task.CompletedTask;
    }

    /// <summary>Hands the messages to the window's lifetime from now on.</summary>
    /// <param name="receive">The lifetime's Receive.</param>
    internal void Deliver(Action<string> receive) => _ = Read(receive);

    private async Task Read(Action<string> receive)
    {
        try
        {
            await foreach (var message in instance.Messages(_stop).ConfigureAwait(false))
            {
                await Dispatcher.UIThread.InvokeAsync(() => receive(message)).GetTask().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
            // Stopped.
        }
    }
}
