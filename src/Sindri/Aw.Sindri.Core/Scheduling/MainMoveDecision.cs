namespace Aw.Sindri.Core.Scheduling;

/// <summary>Stub (red).</summary>
public static class MainMoveDecision
{
    /// <summary>Decides.</summary>
    public static MainMoveAction Decide(IReadOnlyDictionary<string, string> onDefaultHeads, IReadOnlyDictionary<string, string>? lastSucceededHeads, bool running, IReadOnlyList<SyncAttempt> finishedNewestFirst, DateTimeOffset now) =>
        throw new NotSupportedException();

    /// <summary>Counts.</summary>
    public static int FailedInARow(IReadOnlyList<SyncAttempt> finishedNewestFirst) => throw new NotSupportedException();
}
