using Aw.Context.Core.Review;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Commands;

/// <summary>The interactive loop of 'context map review' as a state machine over the queue (the TTY shell only reads lines and sends what it says).</summary>
/// <param name="queue">The queue as the server listed it.</param>
internal sealed class ReviewSession(ReviewQueueView queue)
{
    private readonly ReviewQueueView _queue = queue;

    /// <summary>How many items each action decided or passed over.</summary>
    internal ReviewCounts Counts => throw new NotSupportedException(_queue.Draft);

    /// <summary>The first prompt.</summary>
    /// <returns>The step.</returns>
    internal ReviewStep Start() => throw new NotSupportedException(nameof(ReviewContextMapRequest));

    /// <summary>The human typed <paramref name="line"/>.</summary>
    /// <param name="line">The typed line.</param>
    /// <returns>The step.</returns>
    internal ReviewStep Answer(string line) => throw new NotSupportedException(line);
}
