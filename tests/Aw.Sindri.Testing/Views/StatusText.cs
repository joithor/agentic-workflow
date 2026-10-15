using System.Globalization;
using System.Text;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Testing.Views;

/// <summary>
/// The WF section 6.4 text form of a status, rendered from the fields so the fixtures' <c>text</c> can't disagree with them
/// (API spec section 3.8, "cannot disagree"). The product renderer arrives with API P11 U3; the golden files pin its output.
/// </summary>
public static class StatusText
{
    private const int WrapAt = 100;
    private const string Label = "  ";

    /// <summary>Renders the status the way <c>sindri workflow status</c> prints it.</summary>
    /// <param name="status">The fields (their <c>Text</c> is ignored).</param>
    /// <param name="now">The clock the ages are measured against.</param>
    /// <returns>The lines, each ended by a line feed.</returns>
    public static string Render(StatusFields status, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(status);
        var text = new StringBuilder();
        var h = status.Header;
        var detail = h.Detail is null ? string.Empty : $": {h.Detail}";
        text.Append(CultureInfo.InvariantCulture, $"{h.Address}  {h.Recipe.Value}@{h.Hash12}  v{h.Version}  {h.State}{detail}  since {Clock(h.Since, now)} ({Span(h.AgeSeconds)})\n");
        if (status.Next.Length > 0)
        {
            Line(text, "next", status.Next);
        }

        if (status.Child is { } child)
        {
            Line(text, "child", $"{child.Address}  {child.State}{(child.Reason is null ? string.Empty : $": {child.Reason}")}");
        }

        if (status.Drift is { } drift)
        {
            Line(text, "drift", $"{drift.Block.Value} changed{(drift.NeedsReapproval ? "; the answer needs the re-approval first" : string.Empty)}");
        }

        foreach (var input in status.Inputs)
        {
            Line(text, "inputs", $"{input.Key}  {input.Name}{(input.Hash is null ? string.Empty : $"  sha256:{input.Hash[..4]}…")}");
        }

        if (status.Timeout is { } timeout)
        {
            var kind = timeout.Kind == TimeoutKind.Parks ? "parks" : "escalates";
            Line(text, "timeout", $"{kind} in {Span(timeout.InSeconds)} ({timeout.On})");
        }

        if (status.Budget is { } budget)
        {
            Line(text, "budget", $"tokens {Tokens(budget.Used)} / {Tokens(budget.Limit)} ({budget.Scope}), {Tokens(budget.Instance.Used)} / {Tokens(budget.Instance.Limit)} (instance)");
        }

        if (status.Paused is { } paused)
        {
            Line(text, "paused", $"timeout {Span(paused.TimeoutLeftSeconds)} left");
        }

        if (status.Answer is { } answer)
        {
            Line(text, "answer", answer.Cli + Suffix(status));
        }

        if (status.Commands.Count > 0)
        {
            var verbs = status.Commands.Select(c => c.Verb.Value == "answer" && status.Child is not null ? "answer (forwarded)" : c.Verb.Value);
            Line(text, "commands", string.Join(", ", verbs));
        }

        return text.ToString();
    }

    private static string Suffix(StatusFields status)
    {
        var notes = new List<string>();
        if (status.Answer!.AtTerminal)
        {
            notes.Add("at a terminal");
        }

        if (status.Child is not null)
        {
            notes.Add("forwarded to the child");
        }

        if (status.Answer.Kind == AnswerKind.GrantTokens)
        {
            notes.Add("once per park");
        }

        return notes.Count == 0 ? string.Empty : $"     ({string.Join("; ", notes)})";
    }

    private static void Line(StringBuilder text, string label, string value)
    {
        var first = $"{Label}{label,-10}";
        var pad = new string(' ', first.Length);
        var line = new StringBuilder(first);
        foreach (var word in value.Split(' '))
        {
            if (line.Length > pad.Length && line.Length + 1 + word.Length > WrapAt)
            {
                text.Append(line).Append('\n');
                line.Clear().Append(pad);
            }
            else if (line.Length > pad.Length)
            {
                line.Append(' ');
            }

            line.Append(word);
        }

        text.Append(line).Append('\n');
    }

    private static string Clock(DateTimeOffset at, DateTimeOffset now) =>
        at.Date == now.Date ? at.ToString("HH:mm", CultureInfo.InvariantCulture) : at.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);

    /// <summary>A whole-second duration the way the CLI prints it: '40s', '32m', '3h 16m', '1d 22h 48m'.</summary>
    private static string Span(long seconds)
    {
        if (seconds < 60)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{seconds}s");
        }

        var parts = new List<string>();
        var d = seconds / 86_400;
        var h = seconds % 86_400 / 3_600;
        var m = seconds % 3_600 / 60;
        if (d > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{d}d"));
        }

        if (h > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{h}h"));
        }

        if (m > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{m}m"));
        }

        return string.Join(' ', parts);
    }

    private static string Tokens(long n) => n switch
    {
        >= 1_000_000 => string.Create(CultureInfo.InvariantCulture, $"{n / 1_000_000d:0.##}M"),
        >= 1_000 => string.Create(CultureInfo.InvariantCulture, $"{n / 1_000}k"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{n}"),
    };
}
