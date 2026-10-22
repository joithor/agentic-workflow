namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>A watched pull request and the items whose instances wait on it.</summary>
/// <param name="Link">The pull request's link exactly as 'fact:pr' holds it.</param>
/// <param name="Items">The items, each of which gets the event.</param>
internal sealed record PrSubject(string Link, IReadOnlyList<string> Items);
