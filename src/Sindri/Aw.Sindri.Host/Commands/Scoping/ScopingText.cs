using System.Globalization;
using System.Text;
using Aw.Sindri.Features.Scoping;

namespace Aw.Sindri.Host.Commands.Scoping;

/// <summary>The text 'sindri scoping' prints: a word for every state, no colour.</summary>
internal static class ScopingText
{
    /// <summary>The check's result: the outcome line, each failing finding with its fix, the warnings, and the rules that could not run.</summary>
    internal static string Check(ScopingCheckResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        var text = new StringBuilder();
        var stage = response.Stage == ScopeStage.Draft ? "draft" : "evidence";
        _ = text.Append(CultureInfo.InvariantCulture, $"{stage}: {Word(response.Outcome)}");
        _ = response.Findings.Count > 0 ? text.Append(CultureInfo.InvariantCulture, $" ({response.Findings.Count} findings)") : text;
        _ = text.Append('\n');
        foreach (var finding in response.Findings)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"{finding.Rule} {finding.Item ?? "-"}: {finding.Message} (fix: {finding.Fix})\n");
        }

        foreach (var warning in response.Warnings)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"warning {warning.Rule} {warning.Item ?? "-"}: {warning.Message}\n");
        }

        foreach (var rule in response.Rules.Where(static r => r.Status == ScopeRuleStatus.NotRun && r.Missing.Count > 0))
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"not run: {rule.Rule} (needs {string.Join(", ", rule.Missing)})\n");
        }

        return text.ToString();
    }

    /// <summary>The stop view: the header, the Open in Context action, then the nine sections.</summary>
    internal static string View(ScopingView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var text = new StringBuilder();
        _ = text.Append(CultureInfo.InvariantCulture, $"Scope ({(view.Stage == ScopeStage.Draft ? "draft" : "evidence")}) - {view.CoverageHeader}\n");
        _ = text.Append(view.OpenInContext.Enabled ? "Open in Context: available\n" : $"Open in Context: disabled ({view.OpenInContext.Reason})\n");
        foreach (var section in view.Sections)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"\n{section.Order}. {section.Title}\n");
            if (section.Note is not null)
            {
                _ = text.Append(CultureInfo.InvariantCulture, $"  {section.Note}\n");
            }

            foreach (var row in section.Rows)
            {
                _ = text.Append(CultureInfo.InvariantCulture, $"  {row.Text}\n");
                foreach (var line in row.Lines)
                {
                    _ = text.Append(CultureInfo.InvariantCulture, $"    {line}\n");
                }
            }
        }

        return text.ToString();
    }

    private static string Word(ScopeOutcome outcome) => outcome switch
    {
        ScopeOutcome.Pass => "pass",
        ScopeOutcome.Gaps => "gaps",
        ScopeOutcome.Complete => "complete",
        ScopeOutcome.Incomplete => "incomplete",
        ScopeOutcome.Regather => "regather",
        _ => "unknown",
    };
}
