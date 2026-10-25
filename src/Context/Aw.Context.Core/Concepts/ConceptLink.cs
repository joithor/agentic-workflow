namespace Aw.Context.Core.Concepts;

/// <summary>A link between two concepts, weighted by the member edges between them.</summary>
/// <param name="From">The concept the edges start in.</param>
/// <param name="To">The concept they end in.</param>
/// <param name="Weight">How many edges (other than 'belongs-to') run from a member of <paramref name="From"/> to a member of <paramref name="To"/>.</param>
public sealed record ConceptLink(string From, string To, int Weight);
