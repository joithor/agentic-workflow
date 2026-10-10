using System.Text.Json.Serialization;

namespace Aw.BugFix.Core.Model;

/// <summary>The ticket as the orchestrator wrote it to ticket.json; <see cref="Brief"/> is the verbatim title and description.</summary>
/// <param name="Source">Where the ticket came from.</param>
/// <param name="Reference">The tracker's key (e.g. ENG-123), or null for pasted text. JSON name "id".</param>
/// <param name="Title">The ticket title.</param>
/// <param name="Brief">Title and full description, verbatim; judge reads it as-is.</param>
/// <param name="Expected">The expected behaviour: the acceptance target.</param>
/// <param name="Actual">The observed behaviour.</param>
public sealed record Ticket(
    TicketSource Source,
    [property: JsonPropertyName("id")] string? Reference,
    string Title,
    string Brief,
    string Expected,
    string Actual);
