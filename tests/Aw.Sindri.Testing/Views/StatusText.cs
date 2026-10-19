using System.Globalization;
using System.Text;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Testing.Views;

/// <summary>
/// The WF section 6.4 text form of a status, rendered from the fields so the fixtures' "text" can't disagree with them
/// (API spec section 3.8, "cannot disagree"). The product renderer arrives with API P11 U3; the golden files pin its output.
/// </summary>
public static class StatusText
{
    private const int WrapAt = 120;
    private const string Label = "  ";

    /// <summary>Renders the status the way "sindri workflow status" prints it.</summary>
    /// <param name="status">The fields (their "Text" is ignored).</param>
    /// <param name="now">The clock the ages are measured against.</param>
    /// <param name="name">The instance's rendered name when its recipe has one (WF section 6.4's first labelled line); the fields carry none.</param>
    /// <returns>The lines, each ended by a line feed.</returns>
    public static string Render(StatusFields status, DateTimeOffset now, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(status);
        var text = new StringBuilder();
        var h = status.Header;
        var detail = h.Detail is null ? string.Empty : $": {h.Detail}";
        text.Append(CultureInfo.InvariantCulture, $"{h.Address.Value}  {h.Recipe.Value}@{h.Hash12}  v{h.Version}  {h.State}{detail}  since {Clock(h.Since, now)} ({Span(h.AgeSeconds)})\n");
        if (name is not null)
        {
            Line(text, "name", name);
        }

        foreach (var (label, value) in Rows(status))
        {
            Line(text, label, value);
        }

        return text.ToString();
    }

    private static IEnumerable<(string Label, string Value)> Rows(StatusFields status) =>
        Identity(status).Concat(Limits(status)).Concat(Actions(status));

    private static IEnumerable<(string Label, string Value)> Identity(StatusFields status)
    {
        if (status.Next is { Length: > 0 } next)
        {
            yield return ("next", next);
        }

        if (status.Run is { } run)
        {
            yield return ("run", $"{run.Runtime} {run.Launch}");
        }

        if (status.Child is { } child)
        {
            yield return ("child", $"{child.Address.Value}  {child.State}{(child.Reason is null ? string.Empty : $": {child.Reason}")}");
        }

        if (status.Drift is { } drift)
        {
            yield return ("drift", Drift(status.Header, drift));
        }

        if (status.Inputs.Count > 0)
        {
            // one labelled block: the label sits on the first input and the others are indented under it, like the engine's layout
            yield return ("inputs", string.Join('\n', status.Inputs.Select(input => $"{input.Key}  {input.Name}{(input.Hash is null ? string.Empty : $"  sha256:{Digits(input.Hash)}...")}")));
        }
    }

    private static IEnumerable<(string Label, string Value)> Limits(StatusFields status)
    {
        if (status.Timeout is { } timeout)
        {
            var kind = timeout.Kind == TimeoutKind.Parks ? "parks" : "escalates";
            // Main's StatusLayout prints '(desk)' for the timeout of an open desk task; the 'on' field stays the owning state (API spec 3.8).
            var where = status.Commands.Any(c => c.Verb == StatusVerb.Complete) ? "desk" : timeout.On;
            yield return ("timeout", $"{kind} in {Span(timeout.InSeconds)} ({where})");
        }

        if (status.Budget is { } budget)
        {
            // a null limit is no cap: "used" only, never "/ <limit>"
            var state = budget.Limit is { } cap ? $"{Tokens(budget.Used)} / {Tokens(cap)}" : $"{Tokens(budget.Used)} used";
            var instance = budget.Instance.Limit is { } total ? $"{Tokens(budget.Instance.Used)} / {Tokens(total)}" : $"{Tokens(budget.Instance.Used)} used";
            yield return ("budget", $"tokens {state} ({budget.Scope}), {instance} (instance)");
        }

        if (status.Paused is { } paused)
        {
            yield return ("paused", $"timeout {Span(paused.TimeoutLeftSeconds)} left");
        }
    }

    private static IEnumerable<(string Label, string Value)> Actions(StatusFields status)
    {
        if (status.Answer is { } answer)
        {
            yield return ("answer", answer.Cli + Suffix(status));
        }
        else if (status.Drift is not null)
        {
            yield return ("answer", $"none (drift): sindri recipe approve {Pinned(status.Header)}     (at a terminal; the approval resumes it)");
        }

        if (status.Commands.Count > 0)
        {
            var verbs = status.Commands.Select(c => c.Verb == StatusVerb.Answer && status.Child is not null ? "answer (forwarded)" : StatusVerbNames.Of(c.Verb)).Distinct(StringComparer.Ordinal);
            yield return ("commands", string.Join(", ", verbs));
        }
    }

    private static string Suffix(StatusFields status)
    {
        var notes = new List<string>();
        if (status.Answer!.AtTerminal)
        {
            notes.Add("at a terminal");
        }

        if (status.Child is not null || status.Answer.Via == "child")
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
        var paragraphs = value.Split('\n');
        for (var i = 0; i < paragraphs.Length; i++)
        {
            if (i > 0)
            {
                text.Append(line).Append('\n');
                line.Clear().Append(pad);
            }

            foreach (var word in paragraphs[i].Split(' '))
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
        }

        text.Append(line).Append('\n');
    }

    // WF section 6.4: 'since' adds the date once the age is a day or more.
    private static string Clock(DateTimeOffset at, DateTimeOffset now) =>
        at.UtcDateTime.ToString(now - at >= TimeSpan.FromDays(1) ? "MM-dd HH:mm" : "HH:mm", CultureInfo.InvariantCulture);

    /// <summary>The first four hex digits of a 'sha256:'-prefixed (or bare) digest, as the text form shortens it (WF 6.4).</summary>
    private static string Digits(string hash)
    {
        var digits = hash.StartsWith("sha256:", StringComparison.Ordinal) ? hash["sha256:".Length..] : hash;
        return digits.Length > 4 ? digits[..4] : digits;
    }

    private static string Pinned(StatusHeader header) => $"{header.Recipe.Value}@{header.Hash12}";

    // WF section 6.4: the drift line names the block and the recipe it changed under; the answer line says how it is resolved.
    private static string Drift(StatusHeader header, StatusDrift drift) => $"{drift.Block.Value} changed since {Pinned(header)} was approved";

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

    // The engine's token words (P4b StatusText.Tokens): '950', '1.5k', '200k', '1M', '1.5M'.
    private static string Tokens(long n) => n switch
    {
        > 0 when n % 1_000_000 == 0 => string.Create(CultureInfo.InvariantCulture, $"{n / 1_000_000}M"),
        >= 1_000_000 => string.Create(CultureInfo.InvariantCulture, $"{n / 1_000_000.0:0.#}M"),
        > 0 when n % 1_000 == 0 => string.Create(CultureInfo.InvariantCulture, $"{n / 1_000}k"),
        >= 1_000 => string.Create(CultureInfo.InvariantCulture, $"{n / 1_000.0:0.#}k"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{n}"),
    };
}
