namespace Aw.Sindri.Features.GetEventCatalog;

/// <summary>The public event types.</summary>
/// <param name="Types">One entry per public type.</param>
public sealed record EventCatalogView(IReadOnlyList<EventTypeView> Types);
