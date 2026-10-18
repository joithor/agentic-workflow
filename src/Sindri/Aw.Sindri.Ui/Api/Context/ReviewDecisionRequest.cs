namespace Aw.Sindri.Ui.Api.Context;

/// <summary>The body of POST /sindri/context/map/review: the verb and what it acts on; the wire names are the server's.</summary>
/// <param name="Verb">Verb, as served.</param>
/// <param name="Node">Node, as served.</param>
/// <param name="Link">Link, as served.</param>
/// <param name="Override">Override, as served.</param>
/// <param name="Label">Label, as served.</param>
/// <param name="Other">Other, as served.</param>
/// <param name="Overrides">Overrides, as served.</param>
internal sealed record ReviewDecisionRequest(
    DeskVerb Verb,
    string? Node = null,
    string? Link = null,
    string? Override = null,
    string? Label = null,
    string? Other = null,
    IReadOnlyList<string>? Overrides = null);
