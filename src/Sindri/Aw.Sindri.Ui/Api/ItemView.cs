namespace Aw.Sindri.Ui.Api;

/// <summary>
/// GET /sindri/items/{itemId} (API spec §3.1 GetItem, P3; spec-shaped): the summary plus its version, who created it,
/// what it waits on, the engine instance and its timestamps.
/// </summary>
/// <param name="Id">The item's id.</param>
/// <param name="Title">The title.</param>
/// <param name="Repo">The repo, or null.</param>
/// <param name="State">The ItemState value.</param>
/// <param name="Step">The engine step, or null.</param>
/// <param name="Since">When it entered its state.</param>
/// <param name="PrimaryAction">Its next action id, or null.</param>
/// <param name="Actions">The action ids allowed now.</param>
/// <param name="Body">The body text, scrubbed by the API, or null (only this route serves it).</param>
/// <param name="Link">The tracker record key it's linked to ('linear:issue/…'), or null.</param>
/// <param name="Version">The version the human sees, sent back with CloseItem.</param>
/// <param name="CreatedBy">The actor that created it ('human:cli', 'session:…').</param>
/// <param name="WaitingOn">The decision it waits on, or null.</param>
/// <param name="Instance">The engine instance's address, or null before S2.</param>
/// <param name="CreatedAt">When it was created.</param>
/// <param name="UpdatedAt">When it last changed.</param>
/// <param name="AsOf">How fresh the view is.</param>
/// <param name="Launch">What the app may offer to open, attach or resume it (UI spec E-2); null from a server that predates it.</param>
/// <param name="Owner">Who owns the item (README X61), null for now; no owner UI.</param>
internal sealed record ItemView(
    string Id,
    string Title,
    string? Repo,
    string State,
    string? Step,
    DateTimeOffset Since,
    string? PrimaryAction,
    IReadOnlyList<string> Actions,
    string? Body,
    string? Link,
    long Version,
    string CreatedBy,
    string? WaitingOn,
    string? Instance,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    AsOfView AsOf,
    string? Owner = null,
    ItemLaunchView? Launch = null);
