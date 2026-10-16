namespace Aw.Sindri.Features.GetEventCatalog;

/// <summary>One public event type.</summary>
/// <param name="Type">The type, for example 'item.created'.</param>
/// <param name="KeyShape">Its idempotency key's shape.</param>
/// <param name="Invalidates">The API views the app re-queries when it arrives.</param>
public sealed record EventTypeView(string Type, string KeyShape, IReadOnlyList<string> Invalidates);
