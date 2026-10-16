using System.Runtime.InteropServices;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// Ctrl-C and SIGTERM as a cancelled token, for a verb that runs until it is told to stop ('workflow events --follow') or that holds
/// resources a kill would leave behind ('onyx up' and 'onyx down' run docker children and hold the workspace lock), so it ends with
/// its own exit code instead of the runtime killing the process. Every other verb keeps the default
/// behaviour (Ctrl-C ends it, also at a prompt that is waiting for typed text), so only a verb that
/// <see cref="Streams"/> listens.
/// </summary>
internal sealed class Interrupt : IDisposable
{
    private readonly CancellationTokenSource _source = new();
    private readonly PosixSignalRegistration? _terminate;
    private readonly bool _listening;

    private Interrupt(bool listen)
    {
        _listening = listen;
        if (!listen)
        {
            return;
        }

        Console.CancelKeyPress += OnCancelKey;
        _terminate = PosixSignalRegistration.Create(PosixSignal.SIGTERM, OnTerminate);
    }

    /// <summary>Cancelled by Ctrl-C or SIGTERM; never cancelled for a verb that doesn't stream.</summary>
    public CancellationToken Token => _source.Token;

    /// <summary>Whether <paramref name="args"/> is a verb that listens: 'workflow events … --follow', 'status --follow', 'onyx up' or 'onyx down'.</summary>
    /// <param name="args">The command line.</param>
    /// <returns>True for a following verb.</returns>
    public static bool Streams(string[] args) =>
        (args is ["workflow", "events", .. var rest] && rest.Contains("--follow", StringComparer.Ordinal))
        || (args is ["status", .. var follow] && follow.Contains("--follow", StringComparer.Ordinal))
        || args is ["onyx", "up" or "down", ..];

    /// <summary>Starts listening when <paramref name="args"/> <see cref="Streams"/>.</summary>
    /// <param name="args">The command line.</param>
    /// <returns>The listener; dispose it to stop listening.</returns>
    public static Interrupt For(string[] args) => new(Streams(args));

    public void Dispose()
    {
        if (_listening)
        {
            Console.CancelKeyPress -= OnCancelKey;
            _terminate?.Dispose();
        }

        _source.Dispose();
    }

    private void OnCancelKey(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;   // the verb ends itself; a second Ctrl-C ends the process the usual way
        if (_source.IsCancellationRequested)
        {
            e.Cancel = false;
        }

        _source.Cancel();
    }

    private void OnTerminate(PosixSignalContext context)
    {
        context.Cancel = true;
        _source.Cancel();
    }
}
