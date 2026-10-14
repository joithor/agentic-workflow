using Aw.Context.Core.Results;

namespace Aw.Context.Core.Packing;

/// <summary>An item waiting to be packed.</summary>
/// <param name="Section">Its section name (one of <see cref="ResultSection.FillOrder"/>).</param>
/// <param name="Item">The item, its <see cref="ResultItem.Tokens"/> set.</param>
/// <param name="Marker">Its citation marker, listed under the section's 'more' when it doesn't fit.</param>
public sealed record PackCandidate(string Section, ResultItem Item, string Marker);
