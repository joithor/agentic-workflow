using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.StreamEvents;

/// <summary>
/// Public events after a cursor, for the event stream and any consumer that keeps its own cursor (API spec §5.2). An item or
/// instance filter (engine spec S1-D6) keeps only that item's events, or only the events an instance mirrored (its address-keyed rows).
/// </summary>
/// <param name="After">The last sequence the consumer saw (0 for the start).</param>
/// <param name="Max">The most events to return, 1 to 500.</param>
/// <param name="Item">Only events whose item is this one.</param>
/// <param name="Instance">Only events this instance mirrored into the ledger.</param>
/// <param name="Types">Only these types, or null for every public type (unused until the type filter is real).</param>
public sealed record StreamEventsRequest(long After, int Max = 100, ItemKey? Item = null, InstanceAddress? Instance = null, TypeFilter? Types = null) : IQuery<StreamEventsResponse>;
