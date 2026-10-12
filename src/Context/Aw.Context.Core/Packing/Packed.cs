using Aw.Context.Core.Results;

namespace Aw.Context.Core.Packing;

/// <summary>The packed sections.</summary>
/// <param name="Sections">The non-empty sections, in fill order.</param>
/// <param name="Used">Tokens the packed items take.</param>
/// <param name="Truncated">True when an item was listed as a reference only.</param>
public sealed record Packed(IReadOnlyList<ResultSection> Sections, int Used, bool Truncated);
