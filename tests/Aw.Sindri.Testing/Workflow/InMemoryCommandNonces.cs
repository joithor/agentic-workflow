using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>The in-memory <see cref="ICommandNonces"/> (red: engine P4 unit (c)).</summary>
public sealed class InMemoryCommandNonces : ICommandNonces
{
    /// <summary>A table on <paramref name="clock"/>.</summary>
    /// <param name="clock">The clock the lifetime is read from.</param>
    public InMemoryCommandNonces(TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
    }

    /// <inheritdoc/>
    public CommandNonce Issue() => throw new NotSupportedException("red: engine P4 unit (c)");

    /// <inheritdoc/>
    public ValueTask<Result<bool>> Claim(CommandNonce nonce, string subject, CancellationToken cancellationToken) => throw new NotSupportedException("red: engine P4 unit (c)");

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Applied(CommandNonce nonce, CancellationToken cancellationToken) => throw new NotSupportedException("red: engine P4 unit (c)");

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Release(CommandNonce nonce, CancellationToken cancellationToken) => throw new NotSupportedException("red: engine P4 unit (c)");
}
