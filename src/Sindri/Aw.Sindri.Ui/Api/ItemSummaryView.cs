namespace Aw.Sindri.Ui.Api;

/// <summary>
/// One item as a list shows it (API spec §3.1 ListItems and GetQueue, P3; spec-shaped until P3's goldens land): the app
/// renders these fields as words and never derives a state, a group or an action from them.
/// </summary>
/// <param name="Id">The item's id ('ITM-…').</param>
/// <param name="Title">The title, scrubbed by the API.</param>
/// <param name="Repo">The repo it belongs to, or null.</param>
/// <param name="State">The ItemState value ('new', 'needs-decision', 'running', 'done', …).</param>
/// <param name="Step">The engine step, or null before S2.</param>
/// <param name="Since">When the item entered its state: the clock behind its age.</param>
/// <param name="PrimaryAction">Its next action: a catalog action id, or null.</param>
/// <param name="Actions">The action ids allowed now, in the API's order.</param>
internal sealed record ItemSummaryView(
    string Id,
    string Title,
    string? Repo,
    string State,
    string? Step,
    DateTimeOffset Since,
    string? PrimaryAction,
    IReadOnlyList<string> Actions);
