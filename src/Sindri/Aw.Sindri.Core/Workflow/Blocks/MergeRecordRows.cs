namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>What a merge record holds: its rows in file order, and how many lines could not be read.</summary>
/// <param name="Rows">The rows.</param>
/// <param name="Errors">Lines that were not JSON objects with a kind.</param>
public sealed record MergeRecordRows(IReadOnlyList<MergeRowRead> Rows, int Errors)
{
    /// <summary>A record with nothing in it (no file).</summary>
    public static MergeRecordRows Empty { get; } = new([], 0);
}
