using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Views;
using ActionClass = Aw.Sindri.Core.Commands.ActionClass;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// The status layout as fields (UI screens E-23, API spec §3.8): every line of 'workflow status' as a value, and
/// <see cref="StatusFields.Text"/> the text <see cref="StatusLayout"/> prints for the same view at the same time, so the
/// app and the CLI cannot disagree.
/// </summary>
internal static class StatusRecords
{
    /// <summary>The fields of <paramref name="view"/>, as of <paramref name="now"/>.</summary>
    /// <param name="view">The instance, as built so far (its own <see cref="InstanceView.Status"/> isn't read).</param>
    /// <param name="words">The words of the same instance: the answer line, the drift and the child park as fields.</param>
    /// <param name="now">The time ages and timeouts are measured to.</param>
    /// <returns>The fields.</returns>
    public static StatusFields Of(InstanceView view, InstanceWords words, DateTimeOffset now) => new(
        Header(view, now),
        view.Next.Length == 0 ? null : view.Next,
        words.AnswerLine(),
        words.ChildPark(),
        words.DriftNote(),
        [.. view.Task?.Inputs.Select(static i => new StatusInput(i.Key.Value, i.Value.Name, Hash: Hash(i.Value.Value.Sha256))) ?? []],
        view.Task?.TimeoutAt is { } at ? new StatusTimeout(TimeoutKind.Parks, Seconds(at - now), view.Task.State) : null,
        view is { Task: not null, Budget: { } budget } ? new StatusBudget("state", budget.State, budget.StateCap, new BudgetFigures(budget.Instance, budget.InstanceCap)) : null,
        view.Paused is { } left ? new StatusPaused(Seconds(left)) : null,
        Commands(view, words),
        StatusLayout.Render(view, now));

    private static StatusHeader Header(InstanceView view, DateTimeOffset now) => new(
        view.Address,
        view.Recipe.Id,
        StatusText.Hash12(view.Recipe.Hash.Value),
        view.Version,
        view.State,
        view.Detail,
        view.Since,
        Seconds(now - view.Since));

    /// <summary>Whole seconds, never negative.</summary>
    private static long Seconds(TimeSpan span) => Math.Max(0, (long)span.TotalSeconds);

    private static string Hash(string sha256) => sha256.StartsWith("sha256:", StringComparison.Ordinal) ? sha256 : "sha256:" + sha256;

    /// <summary>The instance's own verbs in §6.4 order, then the re-approval a drift waits for: each a row of the command catalog.</summary>
    private static List<StatusCommand> Commands(InstanceView view, InstanceWords words)
    {
        var rows = view.AllowedCommands.Select(c => Row(c, words.AnswerLine(), view)).ToList();
        if (words.ApproveRow() is { } approve)
        {
            rows.Add(approve);
        }

        return rows;
    }

    private static StatusCommand Row(AllowedCommand command, StatusAnswer? answer, InstanceView view)
    {
        var verb = Verb(StatusLayout.Verb(command.Action));
        return verb switch
        {
            StatusVerb.Answer when answer is not null => new StatusCommand(verb, answer.ActionId, answer.Class, command.Cli, Confirm: null),
            StatusVerb.Answer => new StatusCommand(verb, ActionId.From("ApplyCommand:answer-text"), ActionClass.Desk, command.Cli, Confirm: null),
            StatusVerb.Takeover => new StatusCommand(verb, ActionId.From(command.Action), ActionClass.Pause, command.Cli, Confirm: null),
            StatusVerb.Cancel => new StatusCommand(verb, ActionId.From(command.Action), ActionClass.Desk, command.Cli, ConfirmText.Cancel(view.Address, view.RunningSteps, view.Children.Count)),
            StatusVerb.Complete or StatusVerb.Handback or StatusVerb.Approve or _ => new StatusCommand(verb, ActionId.From(command.Action), ActionClass.Desk, command.Cli, Confirm: null),
        };
    }

    /// <summary>The closed verb of an allowed command's name.</summary>
    private static StatusVerb Verb(string name) => name switch
    {
        "answer" => StatusVerb.Answer,
        "complete" => StatusVerb.Complete,
        "handback" => StatusVerb.Handback,
        "takeover" => StatusVerb.Takeover,
        "cancel" => StatusVerb.Cancel,
        "approve" => StatusVerb.Approve,
        _ => throw new InvalidOperationException($"no status verb '{name}'"),
    };
}
