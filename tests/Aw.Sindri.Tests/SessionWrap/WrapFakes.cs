using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Host.Commands.SessionWrap;

namespace Aw.Sindri.Tests.SessionWrap;

internal sealed class FakeWrapCalls : IWrapCalls
{
    private readonly List<(DateTimeOffset At, SessionId? Session)> _beats = [];
    private readonly List<(int Code, SessionId? Session, string Proof)> _exits = [];
    private readonly TimeProvider _clock;

    public FakeWrapCalls(TimeProvider clock) => _clock = clock;

    public IReadOnlyList<(DateTimeOffset At, SessionId? Session)> Beats { get { lock (_beats) { return [.. _beats]; } } }

    public IReadOnlyList<(int Code, SessionId? Session, string Proof)> Exits { get { lock (_exits) { return [.. _exits]; } } }

    public Error? Refuse { get; set; }

    private Result<Unit> Answer() => Refuse is { } error ? error : Unit.Value;

    public ValueTask<Result<Unit>> Heartbeat(LaunchId launch, LaunchSecret proof, SessionId? session, CancellationToken cancellationToken)
    {
        lock (_beats)
        {
            _beats.Add((_clock.GetUtcNow(), session));
        }

        return ValueTask.FromResult(Answer());
    }

    public ValueTask<Result<Unit>> Exit(LaunchId launch, LaunchSecret proof, int exitCode, SessionId? session, CancellationToken cancellationToken)
    {
        lock (_exits)
        {
            _exits.Add((exitCode, session, proof.Value));
        }

        return ValueTask.FromResult(Answer());
    }
}

internal sealed class FakeChild : IWrapChild
{
    private readonly TaskCompletionSource<int> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<int> Exited => _exited.Task;

    public void Exit(int code) => _exited.TrySetResult(code);

    public void Dispose()
    {
    }
}

internal sealed class FakeChildren : IChildLauncher
{
    public FakeChild? Child { get; private set; }

    public IReadOnlyList<string>? Argv { get; private set; }

    public IReadOnlyDictionary<string, string>? Environment { get; private set; }

    public Result<IWrapChild> Start(IReadOnlyList<string> argv, IReadOnlyDictionary<string, string> environment)
    {
        Argv = argv;
        Environment = environment;
        Child = new FakeChild();
        return Child;
    }
}

internal sealed class FakeIds : ISessionIdSource
{
    private int _looks;

    public Func<int, SessionIdScan> Script { get; set; } = static _ => SessionIdScan.None;

    public int Looks => Volatile.Read(ref _looks);

    public ValueTask<SessionIdScan> Look(LaunchId launch, DateTimeOffset startedAt, TimeSpan elapsed, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Script(Interlocked.Increment(ref _looks)));
}
