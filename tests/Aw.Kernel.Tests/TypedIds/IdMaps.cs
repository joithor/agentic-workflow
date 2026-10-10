namespace Aw.Kernel.Tests.TypedIds;

/// <summary>Maps keyed by each id kind, as a JSON store keyed by id would be.</summary>
internal sealed record IdMaps(
    IReadOnlyDictionary<ItemId, int> Items,
    IReadOnlyDictionary<RunId, int> Runs,
    IReadOnlyDictionary<TokenBudget, int> Budgets);
