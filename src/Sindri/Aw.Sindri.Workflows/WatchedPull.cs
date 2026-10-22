namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>One live instance's pull request.</summary>
/// <param name="Item">The instance's item: the 'pr.changed' event is recorded on it (route 'item').</param>
/// <param name="Link">The instance's 'fact:pr' exactly: the event's subject and the link the forge is asked about.</param>
internal sealed record WatchedPull(string Item, string Link);
