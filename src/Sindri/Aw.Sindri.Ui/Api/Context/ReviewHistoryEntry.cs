namespace Aw.Sindri.Ui.Api.Context;

/// <summary>One override in the review history.</summary>
/// <param name="Id">Id, as served.</param>
/// <param name="Action">Action, as served.</param>
/// <param name="Target">Target, as served.</param>
/// <param name="Link">Link, as served.</param>
/// <param name="Label">Label, as served.</param>
/// <param name="Who">Who, as served.</param>
/// <param name="At">At, as served.</param>
/// <param name="Undone">Undone, as served.</param>
/// <param name="Undoes">Undoes, as served.</param>
internal sealed record ReviewHistoryEntry(
    string Id,
    string Action,
    string Target,
    string? Link,
    string? Label,
    string Who,
    DateTimeOffset At,
    bool Undone,
    string? Undoes);
