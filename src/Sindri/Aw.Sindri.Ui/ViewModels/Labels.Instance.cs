using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the instance frame (screens §4.2). Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The mark between two states of the band.</summary>
    internal const string StepLink = "──";

    /// <summary>The mark between two states that loop into each other.</summary>
    internal const string LoopMark = "⇄";

    /// <summary>The band's heading.</summary>
    internal const string StatesHeading = "States";

    /// <summary>The header note of a band whose recipe orders its states by the transition list.</summary>
    internal const string OrderedByTransitions = "ordered by the transition list, not a path";

    /// <summary>Copies the engine's own status text.</summary>
    internal const string CopyStatus = "Copy status";

    /// <summary>A command the app does not send yet.</summary>
    internal static readonly string AvailableWithU8b = Arrives("U8b");

    /// <summary>What a loop mark says in words: the state loops with the next one.</summary>
    /// <param name="next">The state it loops with.</param>
    /// <returns>The words.</returns>
    internal static string LoopsWith(string next) => $"loops with {next}";

    /// <summary>What a branch's indent says in words.</summary>
    /// <param name="source">The state it branches from.</param>
    /// <returns>The words.</returns>
    internal static string BranchOf(string source) => $"branch of {source}";

    /// <summary>What the frame says after Cancel was confirmed: nothing was sent.</summary>
    internal static readonly string SendingArrivesNote = $"Sending from the app {Arrives("U8b")}. Copy the command and run it at a terminal.";

    /// <summary>The frame's note while its instance is done.</summary>
    internal const string InstanceDone = "This instance is done.";

    /// <summary>The frame's note when a re-read no longer finds the instance it shows.</summary>
    internal const string InstanceGone = "This instance is no longer served; what is shown was its last state.";

    /// <summary>The subject line's lead word.</summary>
    internal const string SubjectLead = "subject";

    /// <summary>The paused state's heading word.</summary>
    internal const string Paused = "paused";

    /// <summary>The line that says the recipe changed under the instance.</summary>
    internal const string NeedsReapproval = "needs re-approval";

    /// <summary>The expanded state's heading for the commands.</summary>
    internal const string CommandsHeading = "commands";

    /// <summary>A frame that has not read its instance yet.</summary>
    internal const string LoadingInstance = "Reading the instance…";

    /// <summary>No instance answers to this address.</summary>
    /// <param name="address">The address asked for.</param>
    /// <returns>The sentence.</returns>
    internal static string InstanceNotFound(string address) => $"No workflow instance {address}.";

    /// <summary>What the frame says when Sindri refuses or can't answer its read.</summary>
    /// <param name="error">The failure as served (a refusal's code and detail, or the client's own).</param>
    /// <returns>The code and the detail.</returns>
    internal static string InstanceRefused(Error error) => $"{error.Code}: {error.Message}";

    /// <summary>The link to a subject outside the app.</summary>
    /// <param name="host">The link's host.</param>
    /// <returns>The words.</returns>
    internal static string OpenIn(string host) => $"Open in {host} ↗";

    /// <summary>The link's accessible name.</summary>
    /// <param name="host">The link's host.</param>
    /// <param name="address">The instance.</param>
    /// <returns>The words.</returns>
    internal static string OpenInName(string host, string address) => $"Open the subject of {address} in {host}";

    /// <summary>Copy status's accessible name.</summary>
    /// <param name="address">The instance.</param>
    /// <returns>The words.</returns>
    internal static string CopyStatusName(string address) => $"{CopyStatus} of {address}";

    /// <summary>The expanded state's accessible name.</summary>
    /// <param name="state">The state.</param>
    /// <param name="address">The instance.</param>
    /// <returns>The words.</returns>
    internal static string ExpandedName(string state, string address) => $"State {state} of {address}";

    /// <summary>A command's button words.</summary>
    /// <param name="verb">The verb.</param>
    /// <param name="answer">The answer's kind when the verb is an answer.</param>
    /// <returns>The words.</returns>
    internal static string CommandLabel(StatusVerb verb, AnswerKind? answer) => verb switch
    {
        StatusVerb.Answer => answer is { } kind ? AnswerVerb(kind) : "Answer",
        StatusVerb.Complete => "Complete",
        StatusVerb.Handback => "Hand back",
        StatusVerb.Takeover => TakeOver,
        StatusVerb.Cancel => CancelStep,
        StatusVerb.Approve => "Approve recipe",
        _ => verb.ToString(),
    };

    /// <summary>A command's accessible name: its words and the instance.</summary>
    /// <param name="label">The words.</param>
    /// <param name="address">The instance.</param>
    /// <param name="chip">The words of the pending chip, which the name carries; empty for none.</param>
    /// <returns>The name.</returns>
    internal static string CommandName(string label, string address, string chip = "") => chip.Length > 0 ? $"{label} {address}, {chip}" : $"{label} {address}";

    /// <summary>The step line of the expanded state.</summary>
    /// <param name="step">What the step declares.</param>
    /// <returns>"kind · ref · declaration".</returns>
    internal static string StepLine(StepDeclaration step) => $"{step.Kind} · {step.Ref} · {step.Declaration}";

    /// <summary>One gate of the expanded state.</summary>
    /// <param name="gate">The gate as served.</param>
    /// <returns>Its id, when it runs, the threshold when there is one, and its description.</returns>
    internal static string GateText(GateLine gate) =>
        $"{gate.Id} on {gate.On}{(gate.Threshold is { } t ? " ≥ " + t.ToString("0.##", CultureInfo.InvariantCulture) : string.Empty)} · {gate.Description}";

    /// <summary>The budget line.</summary>
    /// <param name="budget">The served figures.</param>
    /// <returns>"tokens 1.2M / 4M (state), 1.9M / 6M (instance)".</returns>
    internal static string BudgetLine(StatusBudget budget) =>
        $"tokens {Figure(budget.Used, budget.Limit)} ({budget.Scope}), {Figure(budget.Instance.Used, budget.Instance.Limit)} (instance)";

    /// <summary>The timeout line.</summary>
    /// <param name="timeout">The served timeout.</param>
    /// <returns>"escalates in 20h 48m (build)".</returns>
    internal static string TimeoutLine(StatusTimeout timeout) =>
        $"{(timeout.Kind == TimeoutKind.Parks ? "parks" : "escalates")} in {Duration(timeout.InSeconds)} ({timeout.On})";

    /// <summary>The paused line of a taken-over instance.</summary>
    /// <param name="seconds">How much of the timeout is left.</param>
    /// <returns>The words.</returns>
    internal static string PausedLine(long seconds) => $"{Paused} · {Duration(seconds)} left on the timeout";

    /// <summary>The drift line.</summary>
    /// <param name="drift">The served drift.</param>
    /// <returns>The words.</returns>
    internal static string DriftLine(StatusDrift drift) => $"drift in {drift.Block}{(drift.NeedsReapproval ? " · " + NeedsReapproval : string.Empty)}";

    /// <summary>The child pointer's words.</summary>
    /// <param name="child">The served child.</param>
    /// <returns>The words.</returns>
    internal static string ChildLine(StatusChild child) =>
        $"child {child.Address} · {child.State}{(child.Reason is { Length: > 0 } reason ? ": " + reason : string.Empty)}";

    /// <summary>A length of time as the engine says it: days, hours, minutes, leading zeros left out.</summary>
    /// <param name="seconds">The seconds.</param>
    /// <returns>"1d 23h 34m", "2h 36m", "40s".</returns>
    internal static string Duration(long seconds)
    {
        var span = TimeSpan.FromSeconds(Math.Max(0, seconds));
        if (span.TotalMinutes < 1)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{(int)span.TotalSeconds}s");
        }

        var parts = new List<string>();
        if (span.Days > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{span.Days}d"));
        }

        if (span.Days > 0 || span.Hours > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{span.Hours}h"));
        }

        parts.Add(string.Create(CultureInfo.InvariantCulture, $"{span.Minutes}m"));
        return string.Join(' ', parts);
    }

    // 1.2M / 4M: used and limit, thousands as k and millions as M, a whole figure without a decimal.
    private static string Figure(long used, long? limit) => limit is { } l ? $"{Tokens(used)} / {Tokens(l)}" : Tokens(used);

    private static string Tokens(long n) => n switch
    {
        >= 1_000_000 => (n / 1_000_000d).ToString("0.#", CultureInfo.InvariantCulture) + "M",
        >= 1_000 => (n / 1_000d).ToString("0.#", CultureInfo.InvariantCulture) + "k",
        _ => n.ToString(CultureInfo.InvariantCulture),
    };
}
