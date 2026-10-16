using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>Maps the API's needs-you list, with the command catalog and the pending decisions, onto the rows and feed the view models take.</summary>
internal static class NeedsYouMapper
{
    private const string Decide = "Decide";

    /// <summary>The feed for a served list.</summary>
    /// <param name="list">GET /needs-you.</param>
    /// <param name="catalog">GET /commands: where each action's readiness comes from.</param>
    /// <param name="decisions">The pending decisions, whose options become a decision row's Decide buttons; null keeps the one served action.</param>
    /// <param name="counts">Home's other totals.</param>
    /// <returns>The feed, rows in the API's order.</returns>
    internal static NeedsYouFeed Map(NeedsYouList list, CommandList catalog, DecisionList? decisions, HomeCounts counts)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(catalog);
        var readiness = catalog.Rows.ToDictionary(r => r.Action, r => ReadinessOf(r.Ui), StringComparer.Ordinal);
        return new NeedsYouFeed([.. list.Rows.Select(row => Entry(row, readiness, decisions))], list.Empty ?? string.Empty)
        {
            Stalled = list.AsOf.Stalled,
            Rebuilding = list.AsOf.Rebuilding,
            Counts = counts,
        };
    }

    // The catalog's ui word, read once for the whole app (CatalogReadiness): anything it does not know cannot run.
    internal static ActionReadiness ReadinessOf(string ui) => CatalogReadiness.Parse(ui).Kind switch
    {
        ControlKind.Ready => ActionReadiness.Ready,
        ControlKind.Pending => ActionReadiness.Pending,
        ControlKind.CliOnly => ActionReadiness.CliOnly,
        _ => ActionReadiness.CliOnly,
    };

    private static NeedsYouEntry Entry(NeedsYouRowView row, Dictionary<string, ActionReadiness> readiness, DecisionList? decisions) =>
        new(row.Id, UrgencyOf(row.Urgency))
        {
            Kind = row.Kind,
            Chip = row.Chip ?? Derived(row.Kind),
            ShownId = string.Equals(row.Kind, NeedsYouKinds.Decision, StringComparison.Ordinal) ? row.Subject?.Id ?? string.Empty : row.Item ?? string.Empty,
            Title = row.Title,
            Repo = row.Repo ?? string.Empty,
            Label = row.Label,
            Reason = row.Reason,
            Since = row.Since,
            PrimaryAction = row.PrimaryAction,
            Actions = [.. row.Actions.SelectMany(id => Actions(row, id, readiness, decisions))],
            Subject = row.Subject is { } s ? new NeedsYouSubject(s.Kind, s.Id) : null,
            ItemId = row.Item,
            Cli = row.Cli,
            RecordedOnly = row.RecordedOnly,
            Notice = row.Notice ?? string.Empty,
            Question = QuestionOf(row, decisions),
        };

    private static string QuestionOf(NeedsYouRowView row, DecisionList? decisions) => PendingDecision(row, decisions)?.Question ?? string.Empty;

    // The pending decision a decision row stands for, when the list has it.
    private static DecisionView? PendingDecision(NeedsYouRowView row, DecisionList? decisions) =>
        row.Subject is { } subject && string.Equals(subject.Kind, NeedsYouKinds.Decision, StringComparison.Ordinal)
            ? decisions?.Decisions.FirstOrDefault(d => string.Equals(d.Id, subject.Id, StringComparison.Ordinal))
            : null;

    // A Decide on a decision row is one button per option the pending list offers; every other action is one button as served.
    private static IEnumerable<NeedsYouAction> Actions(NeedsYouRowView row, string id, Dictionary<string, ActionReadiness> readiness, DecisionList? decisions)
    {
        var ready = readiness.GetValueOrDefault(id, ActionReadiness.Pending);
        var decision = string.Equals(id, Decide, StringComparison.Ordinal) ? PendingDecision(row, decisions) : null;
        if (decision is null)
        {
            yield return new NeedsYouAction(id, Labels.Action(id), ready, row.Cli, RejectLike: false);
            yield break;
        }

        foreach (var option in decision.Options)
        {
            yield return new NeedsYouAction(id, option.Label, ready, option.Cli, option.RejectLike) { OptionId = option.Id, Recommended = string.Equals(option.Id, decision.Recommended, StringComparison.Ordinal) };
        }
    }

    // The app maps the served word to a look and never invents urgency: one it does not know still shows, as a pulse.
    private static NeedsYouUrgency UrgencyOf(string urgency) => urgency switch
    {
        "colour" => NeedsYouUrgency.Colour,
        "calm" => NeedsYouUrgency.Calm,
        _ => NeedsYouUrgency.Pulse,
    };

    // Until the API serves a chip on the row (U3'-API-9): the one word for the kind, from the chip table. Documented fallback, not a rule of the app.
    private static StateChip Derived(string kind) => kind switch
    {
        NeedsYouKinds.Decision => new StateChip("DECISION", ChipTone.Attn),
        NeedsYouKinds.Failure => new StateChip("FAILED", ChipTone.Fail),
        NeedsYouKinds.Profile => new StateChip("PROFILE", ChipTone.Attn),
        NeedsYouKinds.Park => new StateChip("PARKED", ChipTone.Attn),
        _ => new StateChip(kind.ToUpperInvariant(), ChipTone.Attn),
    };
}
