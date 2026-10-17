using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using EngineLaunchId = Aw.Sindri.Core.Workflow.Engine.LaunchId;

namespace Aw.Sindri.Testing.Launch;

/// <summary>
/// An <see cref="ILauncher"/> in memory, idempotent per launch id: a repeated Launch returns the first call's Started, and a
/// <see cref="FailNextWith"/> script makes the next new launch fail. Engine P8 and the launcher service test against it.
/// </summary>
public sealed class FakeLauncher : ILauncher
{
    private readonly Dictionary<EngineLaunchId, LaunchResult.Started> _live = [];
    private readonly List<LaunchRequest> _requests = [];
    private readonly List<EngineLaunchId> _stops = [];
    private (Error Error, LaunchStage Stage)? _nextFailure;

    /// <summary>What <see cref="Probe"/> returns.</summary>
    public LaunchProbed Probed { get; set; } = new(
        new TerminalFacts("print", "Print", Present: true, "detected", ConfigPath: null, Problem: null, Detail: null),
        new HostFacts(HostKind.Tmux, Present: true, "3.4", Problem: null), [], []);

    /// <summary>The distinct launches started and not stopped.</summary>
    public int SessionCount => _live.Count;

    /// <summary>Every request Launch received, repeats included, in order.</summary>
    public IReadOnlyList<LaunchRequest> Requests => _requests;

    /// <summary>Every launch id Stop received, repeats included, in order.</summary>
    public IReadOnlyList<EngineLaunchId> Stops => _stops;

    /// <summary>Makes the next new launch fail; a repeat of a live launch still returns its Started.</summary>
    /// <param name="error">The catalogue error.</param>
    /// <param name="stage">Where it stopped.</param>
    public void FailNextWith(Error error, LaunchStage stage = LaunchStage.Host) => _nextFailure = (error, stage);

    /// <inheritdoc/>
    public ValueTask<LaunchResult> Launch(LaunchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        _requests.Add(request);
        if (_live.TryGetValue(request.Launch, out var existing))
        {
            return ValueTask.FromResult<LaunchResult>(existing);
        }

        if (_nextFailure is { } failure)
        {
            _nextFailure = null;
            return ValueTask.FromResult<LaunchResult>(new LaunchResult.Failed(failure.Error, failure.Stage));
        }

        var host = new HostRef(HostKind.Tmux, "snd-" + request.Launch.Value);
        var started = new LaunchResult.Started(
            host, request.Attended ? ViewerOutcome.Opened : ViewerOutcome.Skipped,
            ["tmux", "-L", "sindri", "attach-session", "-t", "=" + host.Name], CatalogSource.Builtin, "fake-argv-hash");
        _live[request.Launch] = started;
        return ValueTask.FromResult<LaunchResult>(started);
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Stop(EngineLaunchId launch, CancellationToken cancellationToken)
    {
        _stops.Add(launch);
        _ = _live.Remove(launch);
        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }

    /// <inheritdoc/>
    public ValueTask<LaunchProbed> Probe(CancellationToken cancellationToken) => ValueTask.FromResult(Probed);
}
