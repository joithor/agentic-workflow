namespace Aw.Context.Core.History;

/// <summary>Records and decisions after the window cut, union and cap.</summary>
/// <param name="Records">The kept records, newest update first.</param>
/// <param name="Decisions">The kept decisions, newest first.</param>
/// <param name="TotalRecords">Records kept before the cap.</param>
/// <param name="TotalDecisions">Decisions kept before the cap.</param>
/// <param name="AfterCut">Records and decisions the 'before' cut dropped.</param>
public sealed record HistoryCutResult(IReadOnlyList<HistoryRecord> Records, IReadOnlyList<HistoryDecision> Decisions, int TotalRecords, int TotalDecisions, int AfterCut)
{
    /// <summary>True when the record list was cut at the cap.</summary>
    public bool RecordsTruncated => TotalRecords > Records.Count;

    /// <summary>True when the decision list was cut at the cap.</summary>
    public bool DecisionsTruncated => TotalDecisions > Decisions.Count;
}
