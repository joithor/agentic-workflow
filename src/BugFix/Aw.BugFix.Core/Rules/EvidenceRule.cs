using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Model;
using Aw.Kernel;

namespace Aw.BugFix.Core.Rules;

/// <summary>Evidence counts only when run-test or run-ui wrote it for this bug fix and nobody changed it since.</summary>
public static class EvidenceRule
{
    /// <summary>Accepts a stored run record.</summary>
    /// <param name="state">The bug fix.</param>
    /// <param name="run">The record as read back.</param>
    /// <returns>The record, or AW-BUGFIX-009.</returns>
    public static Result<RunRecord> Accept(BugFixState state, StoredRun run) =>
        state.Runs.Any(r => r.Evidence == run.Path && r.Sha256 == run.Sha256)
            ? run.Record
            : BugFixErrors.EvidenceRefused.With(
                $"evidence must come from bugfix-state run-test / run-ui for this bug fix ({run.Path} is not a registered, unmodified run record)");
}
