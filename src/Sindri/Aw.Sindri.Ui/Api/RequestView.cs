namespace Aw.Sindri.Ui.Api;

/// <summary>One request of GET /sindri/requests.</summary>
/// <param name="Id">The request id (REQ-…).</param>
/// <param name="Kind">The request's kind.</param>
/// <param name="Item">The item it names, when it names one.</param>
/// <param name="Repo">The repo tag.</param>
/// <param name="Text">What was asked.</param>
/// <param name="Actor">Who filed it.</param>
/// <param name="State">"open", "accepted", "declined" or "voided".</param>
/// <param name="DecisionId">The decision it opened.</param>
/// <param name="FiledAt">When.</param>
internal sealed record RequestView(string Id, string Kind, string? Item, string? Repo, string Text, string Actor, string State, string DecisionId, DateTimeOffset FiledAt);
