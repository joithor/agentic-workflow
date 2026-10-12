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
    // after its 2 s timeout).
    public async ValueTask<bool> Send(string message, CancellationToken cancellationToken) =>
        message.Length is > 0 and <= InstanceMessages.MaxLength
        && await channel.Deliver(message).WaitAsync(cancellationToken).ConfigureAwait(false);

    // The fake has no clock, so a message is never withdrawn: its sender waits until the reader comes back.
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
                try
                {
                    yield return new InstanceMessage(delivery.Message, CancellationToken.None);
                }
                finally
                {
                    // The reader came back for the next one, or stopped after this one: either way it handled it.
                    _ = delivery.Handled.TrySetResult(true);
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
