using Aw.Kernel;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>Claude's records and the fleet's state files whose reads wait until released, and count how often they were started.</summary>
internal sealed class BlockingSources : IClaudeSessionRecords, IFleetStates, IDisposable
{
    private readonly ManualResetEventSlim _gate = new(initialState: false);
    private int _recordReads;
    private int _fleetReads;

    public int RecordReads => Volatile.Read(ref _recordReads);

    public int FleetReads => Volatile.Read(ref _fleetReads);

    public CancellationToken RecordToken { get; private set; }

    public void Release() => _gate.Set();

    Result<IReadOnlyList<ClaudeRecord>> IClaudeSessionRecords.Read(CancellationToken cancellationToken)
    {
        RecordToken = cancellationToken;
        _ = Interlocked.Increment(ref _recordReads);
        _ = _gate.Wait(TimeSpan.FromSeconds(10), CancellationToken.None);
        return Results.Success<IReadOnlyList<ClaudeRecord>>([]);
    }

    Result<IReadOnlyList<FleetStateFile>> IFleetStates.Read(CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _fleetReads);
        _ = _gate.Wait(TimeSpan.FromSeconds(10), CancellationToken.None);
        return Results.Success<IReadOnlyList<FleetStateFile>>([]);
    }

    public int Prune(IReadOnlyCollection<SessionRef> live, DateTimeOffset now) => 0;

    public void Dispose() => _gate.Dispose();
}
