
namespace Aw.Evidence.Core.Data;

/// <summary>What a query returned: its column names, at most the row limit of rows, and whether more were left.</summary>
/// <param name="Columns">The column names, in order.</param>
/// <param name="Values">The rows; each holds one value per column (null, bool, a number, string, bytes, a date or time, or an array).</param>
/// <param name="Truncated">True when the row limit cut the result.</param>
public sealed record Rows(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<object?>> Values, bool Truncated)
{
    /// <summary>The number of rows kept.</summary>
    public int Count => Values.Count;

    /// <summary>The rows as a JSON array of objects keyed by column name.</summary>
    /// <returns>The JSON text.</returns>
    public string ToJson() => throw new NotSupportedException("Rows.ToJson is not written yet");
}
