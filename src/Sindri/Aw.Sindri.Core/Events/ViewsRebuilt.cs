using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'views.rebuilt' payload (API spec §3.4): a rebuild was requested (not public).</summary>
/// <param name="CommandId">The RebuildViews command's id.</param>
/// <param name="CursorBefore">The projector's cursor when the rebuild was requested.</param>
/// <param name="EventsReplayed">The ledger head at the request: what the replay covers.</param>
public sealed record ViewsRebuilt(CommandId CommandId, long CursorBefore, long EventsReplayed);
