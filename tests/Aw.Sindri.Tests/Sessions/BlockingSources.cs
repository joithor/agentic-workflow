using Aw.Kernel;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>Claude's records and the fleet's state files whose reads wait until released, and count how often they were started.</summary>
internal sealed class BlockingSources : IClaudeSessionRecords, IFleetStates, IDisposable
{
    private readonly ManualResetEventSlim _gate = new(initialState: false);
    public ReadSignal Records { get; } = new();

    public ReadSignal Fleet { get; } = new();

    public int RecordReads => Records.Count;

    public int FleetReads => Fleet.Count;

    public CancellationToken RecordToken { get; private set; }

    public void Release() => _gate.Set();

    Result<IReadOnlyList<ClaudeRecord>> IClaudeSessionRecords.Read(CancellationToken cancellationToken)
    {
        RecordToken = cancellationToken;
        Records.Increment();
        _ = _gate.Wait(TimeSpan.FromSeconds(10), CancellationToken.None);
        return Results.Success<IReadOnlyList<ClaudeRecord>>([]);
    }

    Result<IReadOnlyList<FleetStateFile>> IFleetStates.Read(CancellationToken cancellationToken)
    {
        Fleet.Increment();
        _ = _gate.Wait(TimeSpan.FromSeconds(10), CancellationToken.None);
        return Results.Success<IReadOnlyList<FleetStateFile>>([]);
    }

    public int Prune(IReadOnlyCollection<SessionRef> live, DateTimeOffset now) => 0;

    public void Dispose() => _gate.Dispose();
}
