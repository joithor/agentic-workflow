namespace Aw.Sindri.Ui.Api;

/// <summary>One decision of GET /sindri/decisions: pending, decided or voided.</summary>
/// <param name="Id">The decision id (DEC-…).</param>
/// <param name="Kind">The decision's kind ("accept-request").</param>
/// <param name="Class">Always "decision".</param>
/// <param name="Label">The served label.</param>
/// <param name="Item">The item it concerns, when it names one.</param>
/// <param name="ItemVersion">The item's version when the request was filed.</param>
/// <param name="Repo">The repo tag.</param>
/// <param name="Question">What is asked.</param>
/// <param name="Options">The answers on offer, in order.</param>
/// <param name="Recommended">The id of the recommended option.</param>
/// <param name="State">"pending", "decided" or "voided".</param>
/// <param name="FiledBy">Who asked.</param>
/// <param name="FiledAt">When.</param>
/// <param name="RecordedOnly">True when the answer is only recorded now.</param>
/// <param name="Notice">The served recorded-only sentence.</param>
/// <param name="Actions">The catalog ids: ["Decide"] while pending, none after.</param>
/// <param name="Cli">The command to copy.</param>
/// <param name="DecidedBy">Who answered (decided only).</param>
/// <param name="DecidedAt">When (decided only).</param>
/// <param name="Choice">The option chosen (decided only).</param>
/// <param name="Note">The note sent with the answer.</param>
/// <param name="Cleared">The timeline line of a decided or voided decision.</param>
internal sealed record DecisionView(
    string Id,
    string Kind,
    string Class,
    string Label,
    string? Item,
    long ItemVersion,
    string? Repo,
    string Question,
    IReadOnlyList<DecisionOptionView> Options,
    string Recommended,
    string State,
    string FiledBy,
    DateTimeOffset FiledAt,
    bool RecordedOnly,
    string? Notice,
    IReadOnlyList<string> Actions,
    string Cli,
    string? DecidedBy = null,
    DateTimeOffset? DecidedAt = null,
    string? Choice = null,
    string? Note = null,
    string? Cleared = null);
