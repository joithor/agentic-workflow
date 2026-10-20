using System.Globalization;
using System.Text;
using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Host.Commands.Line;

/// <summary>The text 'sindri design|plan check' prints: a word for every state, no colour.</summary>
internal static class LineText
{
    /// <summary>The outcome line, each failing finding with its fix, the warnings, and the rules that could not run.</summary>
    internal static string Check(LineCheckResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        var text = new StringBuilder();
        var stage = response.Stage == LineStage.Draft ? "draft" : "evidence";
        _ = text.Append(CultureInfo.InvariantCulture, $"{response.Station} {stage}: {Word(response.Outcome)}");
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

        foreach (var rule in response.Rules.Where(static r => r.Status == LineRuleStatus.NotRun && r.Missing.Count > 0))
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"not run: {rule.Rule} (needs {string.Join(", ", rule.Missing)})\n");
        }

        return text.ToString();
    }

    private static string Word(LineOutcome outcome) => outcome switch
    {
        LineOutcome.Pass => "pass",
        LineOutcome.Gaps => "gaps",
        LineOutcome.Complete => "complete",
        LineOutcome.Incomplete => "incomplete",
        _ => "unknown",
    };
}
