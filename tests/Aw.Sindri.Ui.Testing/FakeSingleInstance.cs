using System.Runtime.CompilerServices;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Testing;

/// <summary>One launch of the app over a shared <see cref="FakeInstanceChannel"/>.</summary>
/// <param name="channel">The channel every launch of the user shares.</param>
/// <param name="processId">The process id this launch stands for.</param>
internal sealed class FakeSingleInstance(FakeInstanceChannel channel, int processId = 4242) : ISingleInstance
{
    /// <summary>The process id this launch stands for.</summary>
    public int ProcessId { get; } = processId;

    public ValueTask<bool> TryClaim(CancellationToken cancellationToken) => ValueTask.FromResult(channel.TryClaim(this));

    // Handled only while the holder reads its messages; with nobody reading, the fake answers no at once (the adapter
    // after its 2 s timeout). Like the adapter, the answer is whatever settled the delivery first (the app's claim, the
    // reader coming back, or the end of the wait), and the message is withdrawn before the sender hears it.
    public async ValueTask<bool> Send(string message, CancellationToken cancellationToken)
    {
        using var window = channel.HandlingWindow();
        using var waiting = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, window.Token);
        if (message.Length is 0 or > InstanceMessages.MaxLength || channel.Deliver(message) is not { } delivery)
        {
            return false;
        }

        using (delivery)
        {
            try
            {
                _ = await delivery.Handled.Task.WaitAsync(waiting.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // The handling window closed.
            }
            finally
            {
                delivery.Withdraw();
            }

            return await delivery.Handled.Task.ConfigureAwait(false);
        }
    }

    public async IAsyncEnumerable<InstanceMessage> Messages([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (channel.Inbox(this) is not { } inbox)
        {
            yield break;
        }

        try
        {
            await foreach (var delivery in inbox.ReadAllAsync(cancellationToken))
            {
                if (delivery.Handled.Task.IsCompleted)
                {
                    continue; // its sender's wait ended before the reader got to it
                }

                channel.Took();
                try
                {
                    yield return new InstanceMessage(delivery.Message, delivery.TryAct, delivery.Withdrawn);
                }
                finally
                {
                    // The reader came back for the next one, or stopped after this one: either way it handled it, unless
                    // the wait ended first.
                    _ = delivery.TryAct();
                }
            }
        }
        finally
        {
            channel.StopReading(this);
        }
    }

    public int? HolderProcessId() => channel.HolderProcessId;

    public ValueTask DisposeAsync()
    {
        channel.Release(this);
        return ValueTask.CompletedTask;
    }
}
