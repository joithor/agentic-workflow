namespace Aw.Sindri.Ui.Api;

/// <summary>The body of POST /sindri/decisions/{id} (Decide): the choice and an optional note; the id is in the route.</summary>
/// <param name="Choice">The option id.</param>
/// <param name="Note">The note, or null.</param>
internal sealed record DecideRequest(string Choice, string? Note);
