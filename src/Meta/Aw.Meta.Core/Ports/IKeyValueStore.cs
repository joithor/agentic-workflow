using Aw.Kernel;

namespace Aw.Meta.Core.Ports;

/// <summary>Small named values the meta keeps between calls: the stop-check loop guard, the watch heartbeat.</summary>
public interface IKeyValueStore
{
    /// <summary>Reads a value.</summary>
    /// <param name="key">The key.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The value, or null when the key was never set.</returns>
    public ValueTask<Result<string?>> Get(string key, CancellationToken cancellationToken);

    /// <summary>Sets a value, replacing any earlier one.</summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success.</returns>
    public ValueTask<Result<Unit>> Set(string key, string value, CancellationToken cancellationToken);
}
