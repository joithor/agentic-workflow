namespace Aw.Context.Core.Lenses;

/// <summary>What a lenses view was read from.</summary>
/// <param name="Map">The map read ('v13@7c3e1a2').</param>
/// <param name="Draft">True when the draft was read.</param>
/// <param name="AsOf">When the view is built.</param>
public sealed record LensBasis(string Map, bool Draft, DateTimeOffset AsOf);
