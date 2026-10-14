namespace Aw.Context.Core.Results;

/// <summary>One section of a result.</summary>
/// <param name="Name">'graph', 'affects', 'decisions', 'code' or 'records'.</param>
/// <param name="Items">The items that fit the budget, best first.</param>
/// <param name="More">The citation markers of the items that didn't fit, listed as references only ("+N more").</param>
public sealed record ResultSection(string Name, IReadOnlyList<ResultItem> Items, IReadOnlyList<string> More)
{
    /// <summary>The section names, in fill order (spec §8.3).</summary>
    public static IReadOnlyList<string> FillOrder { get; } = ["graph", "affects", "decisions", "code", "records"];
}
