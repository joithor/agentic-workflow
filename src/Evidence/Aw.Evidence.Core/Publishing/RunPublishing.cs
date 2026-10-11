using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Runs;

namespace Aw.Evidence.Core.Publishing;

/// <summary>Plans publishing a run. Unknown provenance hard-gates the Linear upload whatever the answer to the ask.</summary>
public static class RunPublishing
{
    /// <summary>Builds the plan.</summary>
    /// <param name="summary">The run.</param>
    /// <param name="claimed">The provenance the caller claims for the local database.</param>
    /// <param name="hasLinearIssue">Whether a Linear issue was named.</param>
    /// <returns>The plan.</returns>
    public static RunPublishPlan Plan(RunSummary summary, DbProvenance claimed, bool hasLinearIssue)
    {
        var provenance = ProvenanceRules.Effective(summary.Host, claimed);
        var seeded = provenance == DbProvenance.Seeded;
        var question = $"Publish UI evidence for run {summary.RunId.Value}? {Provenance(seeded, hasLinearIssue)}";
        var note = seeded ? null : "DB provenance unknown — evidence kept local only, not uploaded to Linear.";
        var files = summary.Steps.Select(static s => s.Screenshot).ToList();
        return new RunPublishPlan(provenance, question, seeded && hasLinearIssue, files, note, EvidenceComment.Render(summary, note, new Dictionary<string, string>(StringComparer.Ordinal)));
    }

    // The question states what will happen, so it never promises an upload the plan doesn't allow.
    private static string Provenance(bool seeded, bool hasLinearIssue) => (seeded, hasLinearIssue) switch
    {
        (true, true) => "DB provenance: seeded (Linear upload allowed).",
        (true, false) => "DB provenance: seeded, but no Linear issue was given (no Linear upload).",
        _ => "DB provenance: unknown (Linear upload will be skipped; evidence stays local).",
    };
}
