using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Line;

/// <summary>A check run's result at one stage: the failing findings (the 'fact:findings' body) and the warnings beside them.</summary>
/// <param name="Evidence">True for the evidence stage, false for the draft stage.</param>
/// <param name="Findings">Every finding of every rule that ran, failures and warnings.</param>
internal sealed record LineReport(bool Evidence, IReadOnlyList<LineFinding> Findings)
{
    /// <summary>The failing findings.</summary>
    internal IReadOnlyList<LineFinding> Failures => [.. Findings.Where(static f => !f.Warn)];

    /// <summary>Whether the run is clean.</summary>
    internal bool Passed => Failures.Count == 0;

    /// <summary>The outcome name: pass or gaps at the draft stage, complete or incomplete at the evidence stage.</summary>
    internal string Outcome => Evidence ? (Passed ? "complete" : "incomplete") : (Passed ? "pass" : "gaps");

    /// <summary>The failing findings as the 'fact:findings' array (rule, item, message, fix, severity).</summary>
    /// <returns>The array.</returns>
    internal JsonArray FindingsFact()
    {
        var array = new JsonArray();
        foreach (var f in Failures)
        {
            LineJson.Append(array, new JsonObject { ["rule"] = f.Rule, ["item"] = f.Item, ["message"] = f.Message, ["fix"] = f.Fix, ["severity"] = "fail" });
        }

        return array;
    }
}
