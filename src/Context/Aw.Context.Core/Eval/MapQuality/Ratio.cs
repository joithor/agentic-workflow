namespace Aw.Context.Core.Eval.MapQuality;

/// <summary>A count out of a total. With no total the value is null (not measured), never 0.</summary>
/// <param name="Hits">The numerator.</param>
/// <param name="Total">The denominator.</param>
public sealed record Ratio(int Hits, int Total)
{
    /// <summary>Hits over total to four places; null when the total is 0.</summary>
    public double? Value => Total == 0 ? null : Math.Round((double)Hits / Total, 4, MidpointRounding.ToEven);
}
