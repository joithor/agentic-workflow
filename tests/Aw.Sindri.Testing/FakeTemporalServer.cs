using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Testing;

/// <summary>
/// The Temporal dev server in memory: <see cref="Ensure"/> "starts" one (a new pid each time it has to) or adopts the
/// running one, <see cref="Stop"/> takes it down, <see cref="Inspect"/> reports it ready at the pin.
/// </summary>
public sealed class FakeTemporalServer : ITemporalServer
{
    /// <summary>The version this fake's binary reports and pins.</summary>
    public const string Pin = "1.9.1";

    private int _nextPid = 7000;

    /// <summary>The running server, or null.</summary>
    public TemporalEndpoint? Running { get; private set; }

    /// <summary>How many servers <see cref="Ensure"/> started (an adopted one isn't counted).</summary>
    public int Starts { get; private set; }

    /// <summary>How many times <see cref="Stop"/> was called.</summary>
    public int Stops { get; private set; }

    /// <summary>When set, <see cref="Ensure"/> fails with it (and starts nothing).</summary>
    public Error? StartFailure { get; set; }

    /// <inheritdoc/>
    public ValueTask<Result<TemporalEndpoint>> Ensure(CancellationToken cancellationToken)
    {
        if (StartFailure is { } failure)
        {
            return ValueTask.FromResult<Result<TemporalEndpoint>>(failure);
        }

        if (Running is null)
        {
            Starts++;
            _nextPid++;
            Running = new TemporalEndpoint(_nextPid, 17233, 18233);
        }

        return ValueTask.FromResult<Result<TemporalEndpoint>>(Running);
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Stop(CancellationToken cancellationToken)
    {
        Stops++;
        Running = null;
        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }

    /// <inheritdoc/>
    public ValueTask<TemporalFacts> Inspect(CancellationToken cancellationToken) => ValueTask.FromResult(Running is { } running
        ? new TemporalFacts(running, Pin, Pin, Reachable: true, NamespacePresent: true, Problem: null)
        : new TemporalFacts(Running: null, Pin, Version: null, Reachable: false, NamespacePresent: false, Problem: null));

    /// <summary>A start that fails as the adapter does when the binary is missing.</summary>
    /// <returns>The error.</returns>
    public static Error MissingBinary() =>
        SindriErrors.StartFailed.With("the pinned temporal CLI 1.9.1 isn't installed (fix: scripts/binaries-lock.sh fetch temporal)");
}
