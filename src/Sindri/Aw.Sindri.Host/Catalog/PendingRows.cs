using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Host.Catalog;

/// <summary>
/// The rows of API P3–P10a's actions, registered before their phase ships so the app and doctor can list them (P1 plan
/// G7): no command type yet, UI status 'pending:&lt;phase&gt;'. Each phase binds its type and flips its rows to 'ready'.
/// Class, route and CLI come from API spec §3.2 and UI spec §7 (E-1, E-3, E-8); engine and context rows stay with their lanes.
/// </summary>
internal static class PendingRows
{
    internal static IReadOnlyList<CommandRow> All { get; } = [];
}
