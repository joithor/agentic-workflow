namespace Aw.Sindri.Features.OnyxUp;

/// <summary>Waits, before the preflight, for a Docker that is starting or not running yet.</summary>
internal sealed class OnyxDockerWait
{
    /// <summary>How long `onyx up` waits for Docker to answer.</summary>
    internal static readonly TimeSpan Window = TimeSpan.FromMinutes(3);

    /// <summary>The gap between two inspections.</summary>
    internal static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

    /// <summary>Waits until Docker answers, or the window runs out.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that completes when the wait is over.</returns>
    internal ValueTask Wait(CancellationToken cancellationToken) => throw new NotSupportedException();
}
