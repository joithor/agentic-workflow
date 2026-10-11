using System.Runtime.CompilerServices;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Testing;

/// <summary>One launch of the app over a shared <see cref="FakeInstanceChannel"/>.</summary>
/// <param name="channel">The channel every launch of the user shares.</param>
internal sealed class FakeSingleInstance(FakeInstanceChannel channel) : ISingleInstance
{
    public ValueTask<bool> TryClaim(CancellationToken cancellationToken) => ValueTask.FromResult(channel.TryClaim(this));

    public ValueTask<bool> Send(string message, CancellationToken cancellationToken) =>
        ValueTask.FromResult(message.Length is > 0 and <= InstanceMessages.MaxLength && channel.Deliver(message));

    public async IAsyncEnumerable<string> Messages([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (channel.Inbox(this) is not { } inbox)
        {
            yield break;
        }

        await foreach (var message in inbox.ReadAllAsync(cancellationToken))
        {
            yield return message;
        }
    }

    public ValueTask DisposeAsync()
    {
        channel.Release(this);
        return ValueTask.CompletedTask;
    }
}
