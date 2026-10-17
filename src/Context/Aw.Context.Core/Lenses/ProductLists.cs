namespace Aw.Context.Core.Lenses;

/// <summary>One anchor's product lists, uncut.</summary>
internal sealed record ProductLists(List<LensItem> Own, List<LensReach> Neighbors, List<LensReach> Affects, HashSet<string> Seen);
