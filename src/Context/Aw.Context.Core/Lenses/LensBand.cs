namespace Aw.Context.Core.Lenses;

/// <summary>One band of the lenses view: at most 100 items.</summary>
/// <param name="Items">The items.</param>
/// <param name="Truncated">True when more were cut.</param>
public sealed record LensBand(IReadOnlyList<LensItem> Items, bool Truncated);
