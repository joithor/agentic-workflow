using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>The in-memory <see cref="ICommandNonces"/>: issued nonces count up, so a test sees the same ones every run.</summary>
/// <param name="clock">The clock the lifetime is read from.</param>
public sealed class InMemoryCommandNonces(TimeProvider clock) : ICommandNonces
{
    private readonly Dictionary<CommandNonce, (string Subject, DateTimeOffset At, bool Applied)> _claims = [];
    private readonly Lock _gate = new();
    private int _issued;

    /// <inheritdoc/>
    public CommandNonce Issue() =>
        CommandNonce.From(Interlocked.Increment(ref _issued).ToString("x32", CultureInfo.InvariantCulture));

    /// <inheritdoc/>
    public ValueTask<Result<bool>> Claim(CommandNonce nonce, string subject, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var now = clock.GetUtcNow();
            if (!_claims.TryGetValue(nonce, out var claim))
            {
                _claims[nonce] = (subject, now, false);
                return ValueTask.FromResult<Result<bool>>(false);
            }

            return ValueTask.FromResult<Result<bool>>(claim switch
            {
                _ when now - claim.At > ICommandNonces.Lifetime =>
                    WorkflowErrors.NonceRefused.With($"nonce {nonce.Value} expired (fix: run the command again; the CLI fetches a new nonce)"),
                _ when !string.Equals(claim.Subject, subject, StringComparison.Ordinal) =>
                    WorkflowErrors.NonceRefused.With($"nonce {nonce.Value} was used for another command (fix: run the command again; the CLI fetches a new nonce)"),
                _ => claim.Applied,
            });
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Applied(CommandNonce nonce, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_claims.TryGetValue(nonce, out var claim))
            {
                _claims[nonce] = claim with { Applied = true };
            }
        }

        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Release(CommandNonce nonce, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_claims.TryGetValue(nonce, out var claim) && !claim.Applied)
            {
                _ = _claims.Remove(nonce);
            }
        }

        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }
}
