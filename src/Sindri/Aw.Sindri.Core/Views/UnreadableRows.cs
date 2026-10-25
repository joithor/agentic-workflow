namespace Aw.Sindri.Core.Views;

/// <summary>
/// Counts the stored view rows a fold read back and could not deserialize, which happens when a build changes a view record's shape and the
/// views weren't rebuilt. A view store reads such a row as absent and notes it here; the page's <see cref="PageApplied.Unreadable"/> carries
/// the count to the projector, which rebuilds the views from the ledger.
/// </summary>
public sealed class UnreadableRows
{
    /// <summary>How many rows were noted.</summary>
    public int Count { get; private set; }

    /// <summary>Notes one row that could not be read.</summary>
    public void Note() => Count++;
}
