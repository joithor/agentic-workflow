using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.GetEventCatalog;

/// <summary>The public event types with the views each invalidates (GET /sindri/events/catalog; API spec §3.4).</summary>
public sealed record GetEventCatalogRequest() : IQuery<EventCatalogView>;
