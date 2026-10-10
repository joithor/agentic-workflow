namespace Aw.Kernel.Tests.TypedIds;

/// <summary><see cref="IdMaps"/> with plain string keys: what id keys must write exactly like.</summary>
internal sealed record PlainMaps(
    IReadOnlyDictionary<string, int> Items,
    IReadOnlyDictionary<string, int> Runs,
    IReadOnlyDictionary<string, int> Budgets);
